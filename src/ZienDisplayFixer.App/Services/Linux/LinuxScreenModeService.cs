namespace ZienDisplayFixer.App.Services.Linux;

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public class LinuxScreenModeService : IScreenModeService
{
    private readonly IDisplayService? _displayService;
    private ScreenTopology _currentTopology = ScreenTopology.Extend;

    public LinuxScreenModeService(IDisplayService? displayService = null)
    {
        _displayService = displayService;
    }

    public async Task<ScreenTopology> GetCurrentTopologyAsync(CancellationToken ct = default)
    {
        if (_displayService != null)
        {
            try
            {
                var displays = await _displayService.GetDisplaysAsync(ct).ConfigureAwait(false);
                if (displays.Count > 1)
                {
                    bool allAtOrigin = displays.All(d => d.PositionX == 0 && d.PositionY == 0);
                    if (allAtOrigin)
                    {
                        _currentTopology = ScreenTopology.Duplicate;
                        return ScreenTopology.Duplicate;
                    }
                    _currentTopology = ScreenTopology.Extend;
                    return ScreenTopology.Extend;
                }
            }
            catch
            {
                // Fall back to cached topology
            }
        }

        return _currentTopology;
    }

    public async Task SetTopologyAsync(ScreenTopology topology, CancellationToken ct = default)
    {
        _currentTopology = topology;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return;
        }

        try
        {
            var displays = _displayService != null
                ? await _displayService.GetDisplaysAsync(ct).ConfigureAwait(false)
                : Array.Empty<DisplayInfo>();

            var primary = displays.FirstOrDefault(d => d.IsPrimary) ?? displays.FirstOrDefault();
            var secondary = displays.FirstOrDefault(d => d != primary);

            string args;
            if (primary != null && secondary != null)
            {
                args = topology switch
                {
                    ScreenTopology.Duplicate => $"--output {secondary.Id} --same-as {primary.Id}",
                    ScreenTopology.Extend => $"--output {secondary.Id} --right-of {primary.Id}",
                    ScreenTopology.InternalOnly => $"--output {primary.Id} --auto --output {secondary.Id} --off",
                    ScreenTopology.ExternalOnly => $"--output {secondary.Id} --auto --output {primary.Id} --off",
                    _ => $"--output {secondary.Id} --right-of {primary.Id}"
                };
            }
            else
            {
                args = topology switch
                {
                    ScreenTopology.Duplicate => "--same-as",
                    ScreenTopology.Extend => "--auto",
                    _ => "--auto"
                };
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "xrandr",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync(ct).ConfigureAwait(false);
            }
        }
        catch
        {
            // Graceful handling
        }
    }
}

