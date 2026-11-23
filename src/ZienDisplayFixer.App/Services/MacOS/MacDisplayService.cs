namespace ZienDisplayFixer.App.Services.MacOS;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ZienDisplayFixer.App.Services.MacOS.Native;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public class MacDisplayService : IDisplayService, IDisposable
{
    private readonly object _snapshotLock = new();
    private readonly Dictionary<string, MacDisplaySnapshot> _previousModesSnapshot = new(StringComparer.OrdinalIgnoreCase);
    private readonly CoreGraphics.CGDisplayReconfigurationCallBack? _reconfigCallback;
    private GCHandle _reconfigCallbackHandle;
    private bool _isDisposed;

    public event EventHandler? DisplaysChanged;

    public MacDisplayService()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                _reconfigCallback = OnDisplayReconfiguration;
                _reconfigCallbackHandle = GCHandle.Alloc(_reconfigCallback, GCHandleType.Normal);
                CoreGraphics.CGDisplayRegisterReconfigurationCallback(_reconfigCallback, IntPtr.Zero);
            }
            catch
            {
                if (_reconfigCallbackHandle.IsAllocated)
                {
                    _reconfigCallbackHandle.Free();
                }
            }
        }
    }

    private void OnDisplayReconfiguration(uint display, uint flags, IntPtr userInfo)
    {
        // Reconfiguration callback fires before (with kCGDisplayBeginConfigurationFlag)
        // and after changes take effect. Notify after configuration completes.
        if ((flags & CoreGraphics.kCGDisplayBeginConfigurationFlag) == 0)
        {
            DisplaysChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default)
    {
        var displays = new List<DisplayInfo>();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Task.FromResult<IReadOnlyList<DisplayInfo>>(displays);
        }

        uint maxDisplays = 32;
        var activeDisplays = new uint[maxDisplays];
        int err = CoreGraphics.CGGetActiveDisplayList(maxDisplays, activeDisplays, out uint displayCount);
        if (err != CoreGraphics.kCGErrorSuccess || displayCount == 0)
        {
            return Task.FromResult<IReadOnlyList<DisplayInfo>>(displays);
        }

        if (displayCount > maxDisplays)
        {
            maxDisplays = displayCount;
            activeDisplays = new uint[maxDisplays];
            err = CoreGraphics.CGGetActiveDisplayList(maxDisplays, activeDisplays, out displayCount);
            if (err != CoreGraphics.kCGErrorSuccess || displayCount == 0)
            {
                return Task.FromResult<IReadOnlyList<DisplayInfo>>(displays);
            }
        }

        uint mainDisplayId = CoreGraphics.CGMainDisplayID();

        for (int i = 0; i < displayCount && !ct.IsCancellationRequested; i++)
        {
            uint dId = activeDisplays[i];
            bool isPrimary = (dId == mainDisplayId);

            var bounds = CoreGraphics.CGDisplayBounds(dId);
            int posX = (int)Math.Round(bounds.Origin.X);
            int posY = (int)Math.Round(bounds.Origin.Y);

            var physicalSize = CoreGraphics.CGDisplayScreenSize(dId);

            DisplayMode? currentMode = null;
            IntPtr modeRef = CoreGraphics.CGDisplayCopyDisplayMode(dId);
            if (modeRef != IntPtr.Zero)
            {
                try
                {
                    int w = (int)CoreGraphics.CGDisplayModeGetWidth(modeRef);
                    int h = (int)CoreGraphics.CGDisplayModeGetHeight(modeRef);
                    double refresh = CoreGraphics.CGDisplayModeGetRefreshRate(modeRef);
                    if (refresh <= 0.0)
                    {
                        refresh = 60.0;
                    }
                    bool interlaced = (CoreGraphics.CGDisplayModeGetIOFlags(modeRef) & CoreGraphics.kDisplayModeInterlacedFlag) != 0;
                    currentMode = new DisplayMode(w, h, refresh, interlaced);
                }
                finally
                {
                    CoreGraphics.CGDisplayModeRelease(modeRef);
                }
            }

            string idStr = dId.ToString();
            string name = isPrimary ? $"Main Display ({idStr})" : $"Display {idStr}";
            string model = $"Apple Display ({idStr})";

            var info = new DisplayInfo(
                id: idStr,
                name: name,
                model: model,
                isPrimary: isPrimary,
                positionX: posX,
                positionY: posY,
                currentMode: currentMode,
                physicalWidthMm: physicalSize.Width,
                physicalHeightMm: physicalSize.Height,
                connection: ConnectionType.Unknown
            );

            displays.Add(info);
        }

        return Task.FromResult<IReadOnlyList<DisplayInfo>>(displays);
    }

    public Task<IReadOnlyList<DisplayMode>> GetModesAsync(string displayId, CancellationToken ct = default)
    {
        var modes = new List<DisplayMode>();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Task.FromResult<IReadOnlyList<DisplayMode>>(modes);
        }

        if (!uint.TryParse(displayId, out uint dId))
        {
            return Task.FromResult<IReadOnlyList<DisplayMode>>(modes);
        }

        IntPtr optionsDict = CoreGraphics.CreateAllModesOptionsDictionary();
        IntPtr modesArray = IntPtr.Zero;

        try
        {
            modesArray = CoreGraphics.CGDisplayCopyAllDisplayModes(dId, optionsDict);
            if (modesArray == IntPtr.Zero && optionsDict != IntPtr.Zero)
            {
                modesArray = CoreGraphics.CGDisplayCopyAllDisplayModes(dId, IntPtr.Zero);
            }

            if (modesArray != IntPtr.Zero)
            {
                nint count = CoreGraphics.CFArrayGetCount(modesArray);
                for (nint i = 0; i < count && !ct.IsCancellationRequested; i++)
                {
                    IntPtr modeRef = CoreGraphics.CFArrayGetValueAtIndex(modesArray, i);
                    if (modeRef == IntPtr.Zero) continue;

                    int w = (int)CoreGraphics.CGDisplayModeGetWidth(modeRef);
                    int h = (int)CoreGraphics.CGDisplayModeGetHeight(modeRef);
                    if (w <= 0 || h <= 0) continue;

                    double refresh = CoreGraphics.CGDisplayModeGetRefreshRate(modeRef);
                    if (refresh <= 0.0)
                    {
                        refresh = 60.0;
                    }
                    bool interlaced = (CoreGraphics.CGDisplayModeGetIOFlags(modeRef) & CoreGraphics.kDisplayModeInterlacedFlag) != 0;

                    var mode = new DisplayMode(w, h, refresh, interlaced);
                    if (!modes.Contains(mode))
                    {
                        modes.Add(mode);
                    }
                }
            }
        }
        finally
        {
            if (modesArray != IntPtr.Zero)
            {
                CoreGraphics.CFRelease(modesArray);
            }
            if (optionsDict != IntPtr.Zero)
            {
                CoreGraphics.CFRelease(optionsDict);
            }
        }

        return Task.FromResult<IReadOnlyList<DisplayMode>>(modes);
    }

    public async Task<bool> TestModeAsync(string displayId, DisplayMode mode, CancellationToken ct = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return false;
        }

        var modes = await GetModesAsync(displayId, ct).ConfigureAwait(false);
        double targetRefresh = mode.RefreshHz <= 0.0 ? 60.0 : mode.RefreshHz;

        return modes.Any(m =>
            m.Width == mode.Width &&
            m.Height == mode.Height &&
            Math.Abs(m.RefreshHz - targetRefresh) < 0.5 &&
            m.IsInterlaced == mode.IsInterlaced);
    }

    public async Task<bool> ApplyModeAsync(string displayId, DisplayMode mode, CancellationToken ct = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return false;
        }

        if (!uint.TryParse(displayId, out uint targetDisplayId))
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
                    _previousModesSnapshot[d.Id] = new MacDisplaySnapshot(d.Id, d.CurrentMode);
                }
            }
        }

        IntPtr optionsDict = CoreGraphics.CreateAllModesOptionsDictionary();
        IntPtr modesArray = IntPtr.Zero;

        try
        {
            modesArray = CoreGraphics.CGDisplayCopyAllDisplayModes(targetDisplayId, optionsDict);
            if (modesArray == IntPtr.Zero && optionsDict != IntPtr.Zero)
            {
                modesArray = CoreGraphics.CGDisplayCopyAllDisplayModes(targetDisplayId, IntPtr.Zero);
            }

            if (modesArray == IntPtr.Zero)
            {
                return false;
            }

            IntPtr matchingModeRef = FindMatchingModeInArray(modesArray, mode);
            if (matchingModeRef == IntPtr.Zero)
            {
                return false;
            }

            int beginErr = CoreGraphics.CGBeginDisplayConfiguration(out IntPtr configRef);
            if (beginErr != CoreGraphics.kCGErrorSuccess || configRef == IntPtr.Zero)
            {
                return false;
            }

            int configErr = CoreGraphics.CGConfigureDisplayWithDisplayMode(configRef, targetDisplayId, matchingModeRef, IntPtr.Zero);
            if (configErr != CoreGraphics.kCGErrorSuccess)
            {
                CoreGraphics.CGCancelDisplayConfiguration(configRef);
                return false;
            }

            int completeErr = CoreGraphics.CGCompleteDisplayConfiguration(configRef, CoreGraphics.kCGConfigureForSession);
            return completeErr == CoreGraphics.kCGErrorSuccess;
        }
        finally
        {
            if (modesArray != IntPtr.Zero)
            {
                CoreGraphics.CFRelease(modesArray);
            }
            if (optionsDict != IntPtr.Zero)
            {
                CoreGraphics.CFRelease(optionsDict);
            }
        }
    }

    public Task<bool> RestorePreviousModesAsync(CancellationToken ct = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Task.FromResult(false);
        }

        List<MacDisplaySnapshot> snapshotsToRestore;
        lock (_snapshotLock)
        {
            if (_previousModesSnapshot.Count == 0)
            {
                return Task.FromResult(false);
            }
            snapshotsToRestore = _previousModesSnapshot.Values.ToList();
        }

        var arraysToRelease = new List<IntPtr>();
        IntPtr optionsDict = CoreGraphics.CreateAllModesOptionsDictionary();
        if (optionsDict != IntPtr.Zero)
        {
            arraysToRelease.Add(optionsDict);
        }

        try
        {
            int beginErr = CoreGraphics.CGBeginDisplayConfiguration(out IntPtr configRef);
            if (beginErr != CoreGraphics.kCGErrorSuccess || configRef == IntPtr.Zero)
            {
                return Task.FromResult(false);
            }

            bool allConfigured = true;
            foreach (var snapshot in snapshotsToRestore)
            {
                if (!uint.TryParse(snapshot.Id, out uint dId))
                {
                    allConfigured = false;
                    break;
                }

                IntPtr modesArray = CoreGraphics.CGDisplayCopyAllDisplayModes(dId, optionsDict);
                if (modesArray == IntPtr.Zero && optionsDict != IntPtr.Zero)
                {
                    modesArray = CoreGraphics.CGDisplayCopyAllDisplayModes(dId, IntPtr.Zero);
                }

                if (modesArray == IntPtr.Zero)
                {
                    allConfigured = false;
                    break;
                }
                arraysToRelease.Add(modesArray);

                IntPtr matchingMode = FindMatchingModeInArray(modesArray, snapshot.Mode);
                if (matchingMode == IntPtr.Zero)
                {
                    allConfigured = false;
                    break;
                }

                int confErr = CoreGraphics.CGConfigureDisplayWithDisplayMode(configRef, dId, matchingMode, IntPtr.Zero);
                if (confErr != CoreGraphics.kCGErrorSuccess)
                {
                    allConfigured = false;
                    break;
                }
            }

            if (!allConfigured)
            {
                CoreGraphics.CGCancelDisplayConfiguration(configRef);
                return Task.FromResult(false);
            }

            int completeErr = CoreGraphics.CGCompleteDisplayConfiguration(configRef, CoreGraphics.kCGConfigureForSession);
            if (completeErr == CoreGraphics.kCGErrorSuccess)
            {
                lock (_snapshotLock)
                {
                    _previousModesSnapshot.Clear();
                }
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
        finally
        {
            foreach (var ptr in arraysToRelease)
            {
                if (ptr != IntPtr.Zero)
                {
                    CoreGraphics.CFRelease(ptr);
                }
            }
        }
    }

    private static IntPtr FindMatchingModeInArray(IntPtr modesArray, DisplayMode targetMode)
    {
        if (modesArray == IntPtr.Zero) return IntPtr.Zero;

        double targetRefresh = targetMode.RefreshHz <= 0.0 ? 60.0 : targetMode.RefreshHz;
        nint count = CoreGraphics.CFArrayGetCount(modesArray);

        for (nint i = 0; i < count; i++)
        {
            IntPtr candidate = CoreGraphics.CFArrayGetValueAtIndex(modesArray, i);
            if (candidate == IntPtr.Zero) continue;

            int w = (int)CoreGraphics.CGDisplayModeGetWidth(candidate);
            int h = (int)CoreGraphics.CGDisplayModeGetHeight(candidate);
            double refresh = CoreGraphics.CGDisplayModeGetRefreshRate(candidate);
            if (refresh <= 0.0) refresh = 60.0;
            bool interlaced = (CoreGraphics.CGDisplayModeGetIOFlags(candidate) & CoreGraphics.kDisplayModeInterlacedFlag) != 0;

            if (w == targetMode.Width &&
                h == targetMode.Height &&
                Math.Abs(refresh - targetRefresh) < 0.5 &&
                interlaced == targetMode.IsInterlaced)
            {
                return candidate;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                if (_reconfigCallback != null)
                {
                    CoreGraphics.CGDisplayRemoveReconfigurationCallback(_reconfigCallback, IntPtr.Zero);
                }
            }
            catch
            {
                // Ignore during disposal
            }

            if (_reconfigCallbackHandle.IsAllocated)
            {
                _reconfigCallbackHandle.Free();
            }
        }
    }

    ~MacDisplayService()
    {
        Dispose(false);
    }
}

internal sealed record MacDisplaySnapshot(
    string Id,
    DisplayMode Mode);
