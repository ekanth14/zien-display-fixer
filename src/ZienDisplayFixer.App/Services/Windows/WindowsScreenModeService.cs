namespace ZienDisplayFixer.App.Services.Windows;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public class WindowsScreenModeService : IScreenModeService
{
    private readonly IDisplayService? _displayService;
    private ScreenTopology _currentTopology = ScreenTopology.Extend;

    public WindowsScreenModeService(IDisplayService? displayService = null)
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

    public Task SetTopologyAsync(ScreenTopology topology, CancellationToken ct = default)
    {
        _currentTopology = topology;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.CompletedTask;
        }

        string switchArg = topology switch
        {
            ScreenTopology.Extend => "/extend",
            ScreenTopology.Duplicate => "/clone",
            ScreenTopology.InternalOnly => "/internal",
            ScreenTopology.ExternalOnly => "/external",
            _ => "/extend"
        };

        try
        {
            string systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string exePath = Path.Combine(systemPath, "DisplaySwitch.exe");
            if (!File.Exists(exePath))
            {
                exePath = "DisplaySwitch.exe";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = switchArg,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
        }
        catch (Exception)
        {
            // Graceful handling if DisplaySwitch.exe is restricted or fails to launch
        }

        return Task.CompletedTask;
    }
}

