namespace ZienDisplayFixer.App.Services.MacOS;

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ZienDisplayFixer.App.Services.MacOS.Native;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public class MacScreenModeService : IScreenModeService
{
    private readonly IDisplayService? _displayService;
    private ScreenTopology _currentTopology = ScreenTopology.Extend;

    public MacScreenModeService(IDisplayService? displayService = null)
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

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Task.CompletedTask;
        }

        try
        {
            uint maxDisplays = 32;
            var activeDisplays = new uint[maxDisplays];
            int err = CoreGraphics.CGGetActiveDisplayList(maxDisplays, activeDisplays, out uint displayCount);
            if (err == CoreGraphics.kCGErrorSuccess && displayCount > 1)
            {
                uint mainId = CoreGraphics.CGMainDisplayID();
                int configErr = CoreGraphics.CGBeginDisplayConfiguration(out IntPtr configRef);
                if (configErr == CoreGraphics.kCGErrorSuccess && configRef != IntPtr.Zero)
                {
                    for (int i = 0; i < displayCount; i++)
                    {
                        uint dId = activeDisplays[i];
                        if (dId == mainId) continue;

                        if (topology == ScreenTopology.Duplicate)
                        {
                            CoreGraphics.CGConfigureDisplayMirrorOfDisplay(configRef, dId, mainId);
                        }
                        else
                        {
                            // 0 (kCGNullDirectDisplay) to unmirror / extend
                            CoreGraphics.CGConfigureDisplayMirrorOfDisplay(configRef, dId, 0);
                        }
                    }
                    CoreGraphics.CGCompleteDisplayConfiguration(configRef, CoreGraphics.kCGConfigurePermanently);
                }
            }
        }
        catch
        {
            // Graceful error handling
        }

        return Task.CompletedTask;
    }
}
