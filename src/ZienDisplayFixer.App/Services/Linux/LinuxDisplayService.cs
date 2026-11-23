namespace ZienDisplayFixer.App.Services.Linux;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Linux;
using ZienDisplayFixer.Core.Models;

public enum LinuxSessionType
{
    Unknown,
    X11,
    Wayland
}

public enum WaylandTool
{
    None,
    WlrRandr,
    KScreenDoctor,
    GnomeMutterDBus
}

public class LinuxDisplayService : IDisplayService, IDisposable
{
    private readonly object _snapshotLock = new();
    private readonly Dictionary<string, LinuxDisplaySnapshot> _previousModesSnapshot = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer? _pollTimer;
    private string _lastSignature = string.Empty;
    private bool _isDisposed;

    public event EventHandler? DisplaysChanged;

    public LinuxDisplayService()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                _pollTimer = new Timer(OnPollTimerTick, null, TimeSpan.FromSeconds(2.0), TimeSpan.FromSeconds(2.0));
            }
            catch
            {
                // Graceful fallback if timer creation fails
            }
        }
    }

    private async void OnPollTimerTick(object? state)
    {
        if (_isDisposed) return;

        try
        {
            var displays = await GetDisplaysAsync().ConfigureAwait(false);
            string signature = string.Join(";", displays.OrderBy(d => d.Id).Select(d =>
                $"{d.Id}:{d.CurrentMode?.Width}x{d.CurrentMode?.Height}@{d.CurrentMode?.RefreshHz}:{d.PositionX},{d.PositionY}"));

            if (!string.IsNullOrEmpty(_lastSignature) && !string.Equals(_lastSignature, signature, StringComparison.Ordinal))
            {
                DisplaysChanged?.Invoke(this, EventArgs.Empty);
            }

            _lastSignature = signature;
        }
        catch
        {
            // Polling errors should not crash the service
        }
    }

    public static LinuxSessionType DetectSessionType()
    {
        string? sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")?.ToLowerInvariant();
        if (sessionType == "wayland" || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            return LinuxSessionType.Wayland;
        }
        if (sessionType == "x11" || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
        {
            return LinuxSessionType.X11;
        }
        return LinuxSessionType.Unknown;
    }

    public static WaylandTool DetectWaylandTool()
    {
        if (CommandExists("wlr-randr"))
        {
            return WaylandTool.WlrRandr;
        }
        if (CommandExists("kscreen-doctor"))
        {
            return WaylandTool.KScreenDoctor;
        }
        if (CheckGnomeMutterDBusAvailable())
        {
            return WaylandTool.GnomeMutterDBus;
        }
        return WaylandTool.None;
    }

    public async Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default)
    {
        var displays = new List<DisplayInfo>();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return displays;
        }

        var session = DetectSessionType();
        if (session == LinuxSessionType.X11 || CommandExists("xrandr"))
        {
            var (exitCode, stdout, _) = await RunProcessAsync("xrandr", "--query", ct).ConfigureAwait(false);
            if (exitCode == 0)
            {
                return X11Parser.ParseXRandrQuery(stdout);
            }
        }
        else if (session == LinuxSessionType.Wayland)
        {
            var tool = DetectWaylandTool();
            if (tool == WaylandTool.WlrRandr)
            {
                var (exitCode, stdout, _) = await RunProcessAsync("wlr-randr", "", ct).ConfigureAwait(false);
                if (exitCode == 0)
                {
                    return ParseWlrRandrDisplays(stdout);
                }
            }
            else if (CommandExists("xrandr"))
            {
                var (exitCode, stdout, _) = await RunProcessAsync("xrandr", "--query", ct).ConfigureAwait(false);
                if (exitCode == 0)
                {
                    return X11Parser.ParseXRandrQuery(stdout);
                }
            }
        }

        return displays;
    }

    public async Task<IReadOnlyList<DisplayMode>> GetModesAsync(string displayId, CancellationToken ct = default)
    {
        var modes = new List<DisplayMode>();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || string.IsNullOrWhiteSpace(displayId))
        {
            return modes;
        }

        var session = DetectSessionType();
        if (session == LinuxSessionType.X11 || CommandExists("xrandr"))
        {
            var (exitCode, stdout, _) = await RunProcessAsync("xrandr", "--query", ct).ConfigureAwait(false);
            if (exitCode == 0)
            {
                return X11Parser.ParseXRandrModes(stdout, displayId);
            }
        }
        else if (session == LinuxSessionType.Wayland)
        {
            var tool = DetectWaylandTool();
            if (tool == WaylandTool.WlrRandr)
            {
                var (exitCode, stdout, _) = await RunProcessAsync("wlr-randr", "", ct).ConfigureAwait(false);
                if (exitCode == 0)
                {
                    return ParseWlrRandrModes(stdout, displayId);
                }
            }
            else if (CommandExists("xrandr"))
            {
                var (exitCode, stdout, _) = await RunProcessAsync("xrandr", "--query", ct).ConfigureAwait(false);
                if (exitCode == 0)
                {
                    return X11Parser.ParseXRandrModes(stdout, displayId);
                }
            }
        }

        return modes;
    }

    public async Task<bool> TestModeAsync(string displayId, DisplayMode mode, CancellationToken ct = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || string.IsNullOrWhiteSpace(displayId) || mode == null)
        {
            return false;
        }

        var modes = await GetModesAsync(displayId, ct).ConfigureAwait(false);
        return modes.Any(m => m.Width == mode.Width && m.Height == mode.Height && Math.Abs(m.RefreshHz - mode.RefreshHz) < 0.5);
    }

    public async Task<bool> ApplyModeAsync(string displayId, DisplayMode mode, CancellationToken ct = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || string.IsNullOrWhiteSpace(displayId) || mode == null)
        {
            return false;
        }

        var currentDisplays = await GetDisplaysAsync(ct).ConfigureAwait(false);
        if (currentDisplays.Count > 0)
        {
            lock (_snapshotLock)
            {
                _previousModesSnapshot.Clear();
                foreach (var d in currentDisplays)
                {
                    if (d.CurrentMode != null)
                    {
                        _previousModesSnapshot[d.Id] = new LinuxDisplaySnapshot(
                            d.Id,
                            d.CurrentMode,
                            d.PositionX,
                            d.PositionY,
                            d.IsPrimary);
                    }
                }
            }
        }

        var session = DetectSessionType();
        if (session == LinuxSessionType.X11 || CommandExists("xrandr"))
        {
            string rateStr = mode.RefreshHz.ToString("F2", CultureInfo.InvariantCulture);
            string args = $"--output {displayId} --mode {mode.Width}x{mode.Height} --rate {rateStr}";
            var (exitCode, _, _) = await RunProcessAsync("xrandr", args, ct).ConfigureAwait(false);
            return exitCode == 0;
        }
        else if (session == LinuxSessionType.Wayland)
        {
            var tool = DetectWaylandTool();
            switch (tool)
            {
                case WaylandTool.WlrRandr:
                {
                    string rateStr = mode.RefreshHz.ToString("F2", CultureInfo.InvariantCulture);
                    string args = $"--output {displayId} --mode {mode.Width}x{mode.Height} --rate {rateStr}";
                    var (exitCode, _, _) = await RunProcessAsync("wlr-randr", args, ct).ConfigureAwait(false);
                    return exitCode == 0;
                }
                case WaylandTool.KScreenDoctor:
                {
                    string args = $"output.{displayId}.mode.{mode.Width}x{mode.Height}@{Math.Round(mode.RefreshHz)}";
                    var (exitCode, _, _) = await RunProcessAsync("kscreen-doctor", args, ct).ConfigureAwait(false);
                    return exitCode == 0;
                }
                case WaylandTool.GnomeMutterDBus:
                {
                    return await ApplyGnomeMutterModeAsync(displayId, mode, ct).ConfigureAwait(false);
                }
                default:
                    return false;
            }
        }

        return false;
    }

    public async Task<bool> RestorePreviousModesAsync(CancellationToken ct = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return false;
        }

        List<LinuxDisplaySnapshot> snapshots;
        lock (_snapshotLock)
        {
            if (_previousModesSnapshot.Count == 0)
            {
                return false;
            }
            snapshots = _previousModesSnapshot.Values.ToList();
        }

        bool allSucceeded = true;
        var session = DetectSessionType();

        foreach (var snapshot in snapshots)
        {
            if (session == LinuxSessionType.X11 || CommandExists("xrandr"))
            {
                string rateStr = snapshot.Mode.RefreshHz.ToString("F2", CultureInfo.InvariantCulture);
                string args = $"--output {snapshot.Id} --mode {snapshot.Mode.Width}x{snapshot.Mode.Height} --rate {rateStr} --pos {snapshot.PositionX}x{snapshot.PositionY}";
                var (exitCode, _, _) = await RunProcessAsync("xrandr", args, ct).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    allSucceeded = false;
                }
            }
            else
            {
                bool res = await ApplyModeAsync(snapshot.Id, snapshot.Mode, ct).ConfigureAwait(false);
                if (!res)
                {
                    allSucceeded = false;
                }
            }
        }

        if (allSucceeded)
        {
            lock (_snapshotLock)
            {
                _previousModesSnapshot.Clear();
            }
        }

        return allSucceeded;
    }

    private static async Task<bool> ApplyGnomeMutterModeAsync(string displayId, DisplayMode mode, CancellationToken ct)
    {
        if (!CommandExists("gdbus"))
        {
            return false;
        }

        try
        {
            string args = $"call --session --dest org.gnome.Mutter.DisplayConfig --object-path /org/gnome/Mutter/DisplayConfig --method org.gnome.Mutter.DisplayConfig.GetCurrentState";
            var (exitCode, _, _) = await RunProcessAsync("gdbus", args, ct).ConfigureAwait(false);
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool CheckGnomeMutterDBusAvailable()
    {
        if (!CommandExists("gdbus"))
        {
            return false;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gdbus",
                    Arguments = "introspect --session --dest org.gnome.Mutter.DisplayConfig --object-path /org/gnome/Mutter/DisplayConfig",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit(1000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool CommandExists(string command)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit(1000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunProcessAsync(
        string fileName,
        string arguments,
        CancellationToken ct = default)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            string stdout = await stdoutTask.ConfigureAwait(false);
            string stderr = await stderrTask.ConfigureAwait(false);

            return (process.ExitCode, stdout, stderr);
        }
        catch (Exception ex)
        {
            return (-1, string.Empty, ex.Message);
        }
    }

    private static IReadOnlyList<DisplayInfo> ParseWlrRandrDisplays(string output)
    {
        var displays = new List<DisplayInfo>();
        if (string.IsNullOrWhiteSpace(output)) return displays;

        var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        string currentName = string.Empty;
        string currentModel = string.Empty;
        int posX = 0, posY = 0;
        double physW = 0, physH = 0;
        DisplayMode? currentMode = null;

        foreach (var line in lines)
        {
            if (!line.StartsWith(" ") && !line.StartsWith("\t") && !string.IsNullOrWhiteSpace(line))
            {
                if (!string.IsNullOrEmpty(currentName))
                {
                    displays.Add(new DisplayInfo(
                        id: currentName,
                        name: currentName,
                        model: !string.IsNullOrEmpty(currentModel) ? currentModel : currentName,
                        isPrimary: displays.Count == 0,
                        positionX: posX,
                        positionY: posY,
                        currentMode: currentMode,
                        physicalWidthMm: physW,
                        physicalHeightMm: physH,
                        connection: DetectConnectionType(currentName)
                    ));
                }

                var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                currentName = parts.Length > 0 ? parts[0] : string.Empty;
                currentModel = parts.Length > 1 ? parts[1].Trim('\"', ' ') : currentName;
                posX = 0; posY = 0;
                physW = 0; physH = 0;
                currentMode = null;
            }
            else
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("Position:"))
                {
                    var posPart = trimmed.Substring("Position:".Length).Trim();
                    var coords = posPart.Split(',');
                    if (coords.Length == 2 && int.TryParse(coords[0].Trim(), out int px) && int.TryParse(coords[1].Trim(), out int py))
                    {
                        posX = px; posY = py;
                    }
                }
                else if (trimmed.StartsWith("Physical size:"))
                {
                    var sizePart = trimmed.Substring("Physical size:".Length).Replace("mm", "").Trim();
                    var dims = sizePart.Split('x');
                    if (dims.Length == 2 && double.TryParse(dims[0].Trim(), CultureInfo.InvariantCulture, out double pw) &&
                        double.TryParse(dims[1].Trim(), CultureInfo.InvariantCulture, out double ph))
                    {
                        physW = pw; physH = ph;
                    }
                }
                else if (trimmed.Contains("current"))
                {
                    var modeMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"(?<w>\d+)x(?<h>\d+)\s+px,\s+(?<rate>\d+(?:\.\d+)?)\s+Hz");
                    if (modeMatch.Success)
                    {
                        int w = int.Parse(modeMatch.Groups["w"].Value, CultureInfo.InvariantCulture);
                        int h = int.Parse(modeMatch.Groups["h"].Value, CultureInfo.InvariantCulture);
                        double r = double.Parse(modeMatch.Groups["rate"].Value, CultureInfo.InvariantCulture);
                        currentMode = new DisplayMode(w, h, r);
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(currentName))
        {
            displays.Add(new DisplayInfo(
                id: currentName,
                name: currentName,
                model: !string.IsNullOrEmpty(currentModel) ? currentModel : currentName,
                isPrimary: displays.Count == 0,
                positionX: posX,
                positionY: posY,
                currentMode: currentMode,
                physicalWidthMm: physW,
                physicalHeightMm: physH,
                connection: DetectConnectionType(currentName)
            ));
        }

        return displays;
    }

    private static IReadOnlyList<DisplayMode> ParseWlrRandrModes(string output, string displayName)
    {
        var modes = new List<DisplayMode>();
        if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(displayName)) return modes;

        var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        bool inTarget = false;

        foreach (var line in lines)
        {
            if (!line.StartsWith(" ") && !line.StartsWith("\t") && !string.IsNullOrWhiteSpace(line))
            {
                var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                inTarget = parts.Length > 0 && string.Equals(parts[0], displayName, StringComparison.OrdinalIgnoreCase);
            }
            else if (inTarget)
            {
                var modeMatch = System.Text.RegularExpressions.Regex.Match(line.Trim(), @"(?<w>\d+)x(?<h>\d+)\s+px,\s+(?<rate>\d+(?:\.\d+)?)\s+Hz");
                if (modeMatch.Success)
                {
                    int w = int.Parse(modeMatch.Groups["w"].Value, CultureInfo.InvariantCulture);
                    int h = int.Parse(modeMatch.Groups["h"].Value, CultureInfo.InvariantCulture);
                    double r = double.Parse(modeMatch.Groups["rate"].Value, CultureInfo.InvariantCulture);
                    var mode = new DisplayMode(w, h, r);
                    if (!modes.Contains(mode))
                    {
                        modes.Add(mode);
                    }
                }
            }
        }

        return modes;
    }

    private static ConnectionType DetectConnectionType(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName)) return ConnectionType.Unknown;
        if (portName.StartsWith("HDMI", StringComparison.OrdinalIgnoreCase)) return ConnectionType.Hdmi;
        if (portName.StartsWith("eDP", StringComparison.OrdinalIgnoreCase) || portName.StartsWith("DP", StringComparison.OrdinalIgnoreCase)) return ConnectionType.DisplayPort;
        if (portName.StartsWith("VGA", StringComparison.OrdinalIgnoreCase)) return ConnectionType.Vga;
        if (portName.StartsWith("USB", StringComparison.OrdinalIgnoreCase)) return ConnectionType.UsbC;
        return ConnectionType.Unknown;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _pollTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}

internal sealed record LinuxDisplaySnapshot(
    string Id,
    DisplayMode Mode,
    int PositionX,
    int PositionY,
    bool IsPrimary);
