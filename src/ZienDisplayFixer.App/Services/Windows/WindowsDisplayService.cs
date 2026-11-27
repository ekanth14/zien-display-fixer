namespace ZienDisplayFixer.App.Services.Windows;

using System.Runtime.InteropServices;
using Microsoft.Win32;
using ZienDisplayFixer.App.Services.Windows.Native;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public class WindowsDisplayService : IDisplayService, IDisposable
{
    private readonly object _snapshotLock = new();
    private readonly Dictionary<string, DisplaySnapshot> _previousModesSnapshot = new(StringComparer.OrdinalIgnoreCase);
    private bool _isDisposed;

    public event EventHandler? DisplaysChanged;

    public WindowsDisplayService()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            }
            catch
            {
                // Graceful fallback if system events are not accessible
            }
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        DisplaysChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default)
    {
        var displays = new List<DisplayInfo>();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.FromResult<IReadOnlyList<DisplayInfo>>(displays);
        }

        uint devNum = 0;
        while (!ct.IsCancellationRequested)
        {
            var adapterDevice = new DISPLAY_DEVICEW();
            adapterDevice.cb = (uint)Marshal.SizeOf<DISPLAY_DEVICEW>();

            if (!User32.EnumDisplayDevices(null, devNum, ref adapterDevice, 0))
            {
                break;
            }

            if ((adapterDevice.StateFlags & User32.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0)
            {
                bool isPrimary = (adapterDevice.StateFlags & User32.DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;
                string deviceName = adapterDevice.DeviceName;
                string modelName = adapterDevice.DeviceString;
                string hardwareId = string.Empty;

                var monitorDevice = new DISPLAY_DEVICEW();
                monitorDevice.cb = (uint)Marshal.SizeOf<DISPLAY_DEVICEW>();
                if (User32.EnumDisplayDevices(deviceName, 0, ref monitorDevice, 0))
                {
                    if (!string.IsNullOrWhiteSpace(monitorDevice.DeviceString))
                    {
                        modelName = monitorDevice.DeviceString;
                    }
                    hardwareId = ParseHardwareId(monitorDevice.DeviceID);
                }

                var devMode = new DEVMODEW();
                devMode.dmSize = (ushort)Marshal.SizeOf<DEVMODEW>();

                DisplayMode? currentMode = null;
                int posX = 0;
                int posY = 0;

                if (User32.EnumDisplaySettingsEx(deviceName, User32.ENUM_CURRENT_SETTINGS, ref devMode, 0))
                {
                    posX = devMode.dmPositionX;
                    posY = devMode.dmPositionY;
                    currentMode = new DisplayMode(
                        (int)devMode.dmPelsWidth,
                        (int)devMode.dmPelsHeight,
                        devMode.dmDisplayFrequency,
                        (devMode.dmDisplayFlags & User32.DM_INTERLACED) != 0
                    );
                }

                var info = new DisplayInfo(
                    id: deviceName,
                    name: !string.IsNullOrWhiteSpace(modelName) ? modelName : deviceName,
                    model: modelName,
                    isPrimary: isPrimary,
                    positionX: posX,
                    positionY: posY,
                    currentMode: currentMode,
                    physicalWidthMm: 0,
                    physicalHeightMm: 0,
                    connection: ConnectionType.Unknown
                )
                {
                    HardwareId = hardwareId
                };

                displays.Add(info);
            }

            devNum++;
        }

        return Task.FromResult<IReadOnlyList<DisplayInfo>>(displays);
    }

    public Task<IReadOnlyList<DisplayMode>> GetModesAsync(string displayId, CancellationToken ct = default)
    {
        var modes = new List<DisplayMode>();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.FromResult<IReadOnlyList<DisplayMode>>(modes);
        }

        int modeNum = 0;
        while (!ct.IsCancellationRequested)
        {
            var devMode = new DEVMODEW();
            devMode.dmSize = (ushort)Marshal.SizeOf<DEVMODEW>();

            if (!User32.EnumDisplaySettingsEx(displayId, modeNum, ref devMode, 0))
            {
                break;
            }

            if (devMode.dmPelsWidth > 0 && devMode.dmPelsHeight > 0)
            {
                var mode = new DisplayMode(
                    (int)devMode.dmPelsWidth,
                    (int)devMode.dmPelsHeight,
                    devMode.dmDisplayFrequency,
                    (devMode.dmDisplayFlags & User32.DM_INTERLACED) != 0
                );

                if (!modes.Contains(mode))
                {
                    modes.Add(mode);
                }
            }

            modeNum++;
        }

        return Task.FromResult<IReadOnlyList<DisplayMode>>(modes);
    }

    public Task<bool> TestModeAsync(string displayId, DisplayMode mode, CancellationToken ct = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.FromResult(false);
        }

        var devMode = new DEVMODEW();
        devMode.dmSize = (ushort)Marshal.SizeOf<DEVMODEW>();
        devMode.dmPelsWidth = (uint)mode.Width;
        devMode.dmPelsHeight = (uint)mode.Height;
        devMode.dmDisplayFrequency = (uint)Math.Round(mode.RefreshHz);
        devMode.dmFields = User32.DM_PELSWIDTH | User32.DM_PELSHEIGHT | User32.DM_DISPLAYFREQUENCY | User32.DM_DISPLAYFLAGS;
        devMode.dmDisplayFlags = mode.IsInterlaced ? User32.DM_INTERLACED : 0;

        int result = User32.ChangeDisplaySettingsEx(displayId, ref devMode, IntPtr.Zero, User32.CDS_TEST, IntPtr.Zero);
        return Task.FromResult(result == User32.DISP_CHANGE_SUCCESSFUL);
    }

    public async Task<bool> ApplyModeAsync(string displayId, DisplayMode mode, CancellationToken ct = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        var displays = await GetDisplaysAsync(ct).ConfigureAwait(false);
        if (displays.Count == 0)
        {
            return false;
        }

        lock (_snapshotLock)
        {
            _previousModesSnapshot.Clear();
            foreach (var d in displays)
            {
                if (d.CurrentMode != null)
                {
                    _previousModesSnapshot[d.Id] = new DisplaySnapshot(
                        d.Id,
                        d.CurrentMode,
                        d.PositionX,
                        d.PositionY,
                        d.IsPrimary);
                }
            }
        }

        var targetLayouts = CalculateLayout(displays, displayId, mode);

        foreach (var target in targetLayouts)
        {
            var devMode = new DEVMODEW();
            devMode.dmSize = (ushort)Marshal.SizeOf<DEVMODEW>();
            devMode.dmPelsWidth = (uint)target.TargetWidth;
            devMode.dmPelsHeight = (uint)target.TargetHeight;
            devMode.dmDisplayFrequency = target.TargetRefreshHz;
            devMode.dmPositionX = target.PositionX;
            devMode.dmPositionY = target.PositionY;
            devMode.dmFields = User32.DM_PELSWIDTH | User32.DM_PELSHEIGHT | User32.DM_DISPLAYFREQUENCY | User32.DM_POSITION | User32.DM_DISPLAYFLAGS;
            devMode.dmDisplayFlags = target.IsInterlaced ? User32.DM_INTERLACED : 0;

            uint flags = User32.CDS_UPDATEREGISTRY | User32.CDS_NORESET;
            if (target.IsPrimary)
            {
                flags |= User32.CDS_SET_PRIMARY;
            }

            int res = User32.ChangeDisplaySettingsEx(target.DisplayId, ref devMode, IntPtr.Zero, flags, IntPtr.Zero);
            if (res != User32.DISP_CHANGE_SUCCESSFUL)
            {
                return false;
            }
        }

        int commitRes = User32.ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
        return commitRes == User32.DISP_CHANGE_SUCCESSFUL;
    }

    public async Task<bool> ApplyConfigurationAsync(IReadOnlyList<DisplayTarget> targets, CancellationToken ct = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || targets.Count == 0)
        {
            return false;
        }

        var displays = await GetDisplaysAsync(ct).ConfigureAwait(false);
        if (displays.Count == 0)
        {
            return false;
        }

        lock (_snapshotLock)
        {
            _previousModesSnapshot.Clear();
            foreach (var d in displays)
            {
                if (d.CurrentMode != null)
                {
                    _previousModesSnapshot[d.Id] = new DisplaySnapshot(d.Id, d.CurrentMode, d.PositionX, d.PositionY, d.IsPrimary);
                }
            }
        }

        // Every display gets its mode and position staged in the registry, then one commit applies them together,
        // so Windows never sees a half-applied layout with gaps it would "fix" on its own.
        foreach (var target in targets)
        {
            var devMode = new DEVMODEW();
            devMode.dmSize = (ushort)Marshal.SizeOf<DEVMODEW>();
            devMode.dmPelsWidth = (uint)target.Mode.Width;
            devMode.dmPelsHeight = (uint)target.Mode.Height;
            devMode.dmDisplayFrequency = (uint)Math.Round(target.Mode.RefreshHz);
            devMode.dmPositionX = target.PositionX;
            devMode.dmPositionY = target.PositionY;
            devMode.dmFields = User32.DM_PELSWIDTH | User32.DM_PELSHEIGHT | User32.DM_DISPLAYFREQUENCY | User32.DM_POSITION | User32.DM_DISPLAYFLAGS;
            devMode.dmDisplayFlags = target.Mode.IsInterlaced ? User32.DM_INTERLACED : 0;

            uint flags = User32.CDS_UPDATEREGISTRY | User32.CDS_NORESET;
            if (target.IsPrimary)
            {
                flags |= User32.CDS_SET_PRIMARY;
            }

            int res = User32.ChangeDisplaySettingsEx(target.DisplayId, ref devMode, IntPtr.Zero, flags, IntPtr.Zero);
            if (res != User32.DISP_CHANGE_SUCCESSFUL)
            {
                await RestorePreviousModesAsync(ct).ConfigureAwait(false);
                return false;
            }
        }

        int commitRes = User32.ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
        return commitRes == User32.DISP_CHANGE_SUCCESSFUL;
    }

    public static string ParseHardwareId(string? monitorDeviceId)
    {
        // "MONITOR\HEC0030\{4d36e96e-...}\0006" -> "HEC0030"
        if (string.IsNullOrWhiteSpace(monitorDeviceId))
        {
            return string.Empty;
        }

        var parts = monitorDeviceId.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[1] : string.Empty;
    }

    public Task<bool> RestorePreviousModesAsync(CancellationToken ct = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.FromResult(false);
        }

        List<DisplaySnapshot> snapshotsToRestore;
        lock (_snapshotLock)
        {
            if (_previousModesSnapshot.Count == 0)
            {
                return Task.FromResult(false);
            }
            snapshotsToRestore = _previousModesSnapshot.Values.ToList();
        }

        foreach (var s in snapshotsToRestore)
        {
            var devMode = new DEVMODEW();
            devMode.dmSize = (ushort)Marshal.SizeOf<DEVMODEW>();
            devMode.dmPelsWidth = (uint)s.Mode.Width;
            devMode.dmPelsHeight = (uint)s.Mode.Height;
            devMode.dmDisplayFrequency = (uint)Math.Round(s.Mode.RefreshHz);
            devMode.dmPositionX = s.PositionX;
            devMode.dmPositionY = s.PositionY;
            devMode.dmFields = User32.DM_PELSWIDTH | User32.DM_PELSHEIGHT | User32.DM_DISPLAYFREQUENCY | User32.DM_POSITION | User32.DM_DISPLAYFLAGS;
            devMode.dmDisplayFlags = s.Mode.IsInterlaced ? User32.DM_INTERLACED : 0;

            uint flags = User32.CDS_UPDATEREGISTRY | User32.CDS_NORESET;
            if (s.IsPrimary)
            {
                flags |= User32.CDS_SET_PRIMARY;
            }

            int res = User32.ChangeDisplaySettingsEx(s.Id, ref devMode, IntPtr.Zero, flags, IntPtr.Zero);
            if (res != User32.DISP_CHANGE_SUCCESSFUL)
            {
                return Task.FromResult(false);
            }
        }

        int commitRes = User32.ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
        if (commitRes == User32.DISP_CHANGE_SUCCESSFUL)
        {
            lock (_snapshotLock)
            {
                _previousModesSnapshot.Clear();
            }
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public static IReadOnlyList<DisplayTargetLayout> CalculateLayout(
        IReadOnlyList<DisplayInfo> displays,
        string targetDisplayId,
        DisplayMode newMode)
    {
        if (displays == null || displays.Count == 0)
        {
            return Array.Empty<DisplayTargetLayout>();
        }

        var targets = new List<DisplayTargetLayout>(displays.Count);
        for (int i = 0; i < displays.Count; i++)
        {
            var d = displays[i];
            bool isTarget = string.Equals(d.Id, targetDisplayId, StringComparison.OrdinalIgnoreCase);
            int width = isTarget ? newMode.Width : (d.CurrentMode?.Width ?? 1920);
            int height = isTarget ? newMode.Height : (d.CurrentMode?.Height ?? 1080);
            uint refresh = isTarget ? (uint)Math.Round(newMode.RefreshHz) : (uint)Math.Round(d.CurrentMode?.RefreshHz ?? 60.0);
            bool interlaced = isTarget ? newMode.IsInterlaced : (d.CurrentMode?.IsInterlaced ?? false);

            targets.Add(new DisplayTargetLayout(
                d.Id,
                width,
                height,
                refresh,
                interlaced,
                d.PositionX,
                d.PositionY,
                d.IsPrimary));
        }

        bool hasOverlap = false;
        for (int i = 0; i < targets.Count && !hasOverlap; i++)
        {
            var a = targets[i];
            for (int j = i + 1; j < targets.Count; j++)
            {
                var b = targets[j];
                if (Overlaps(a.PositionX, a.PositionY, a.TargetWidth, a.TargetHeight,
                             b.PositionX, b.PositionY, b.TargetWidth, b.TargetHeight))
                {
                    hasOverlap = true;
                    break;
                }
            }
        }

        if (!hasOverlap)
        {
            return targets;
        }

        // Re-tile displays left-to-right maintaining original spatial order
        var sortedTargets = targets.OrderBy(t => t.PositionX).ToList();
        var retiled = new List<DisplayTargetLayout>(sortedTargets.Count);
        int currentX = 0;
        int primaryIndex = -1;

        for (int i = 0; i < sortedTargets.Count; i++)
        {
            if (sortedTargets[i].IsPrimary)
            {
                primaryIndex = i;
            }
        }
        if (primaryIndex < 0) primaryIndex = 0;

        var relativePositions = new int[sortedTargets.Count];
        for (int i = 0; i < sortedTargets.Count; i++)
        {
            relativePositions[i] = currentX;
            currentX += sortedTargets[i].TargetWidth;
        }

        int primaryOffset = relativePositions[primaryIndex];
        for (int i = 0; i < sortedTargets.Count; i++)
        {
            var t = sortedTargets[i];
            retiled.Add(t with
            {
                PositionX = relativePositions[i] - primaryOffset,
                PositionY = 0
            });
        }

        return retiled;
    }

    private static bool Overlaps(int ax, int ay, int aw, int ah, int bx, int by, int bw, int bh)
    {
        return ax < bx + bw && ax + aw > bx &&
               ay < by + bh && ay + ah > by;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            }
            catch
            {
                // Ignore during disposal
            }
        }
        GC.SuppressFinalize(this);
    }
}

public sealed record DisplayTargetLayout(
    string DisplayId,
    int TargetWidth,
    int TargetHeight,
    uint TargetRefreshHz,
    bool IsInterlaced,
    int PositionX,
    int PositionY,
    bool IsPrimary);

internal sealed record DisplaySnapshot(
    string Id,
    DisplayMode Mode,
    int PositionX,
    int PositionY,
    bool IsPrimary);
