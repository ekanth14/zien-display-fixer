namespace ZienDisplayFixer.Core.Engine;

using ZienDisplayFixer.Core.Models;

public static class BestModePicker
{
    // Windows reports 59.94 Hz as 59, so 59 is the floor for a smooth desktop.
    public const double SmoothRefreshFloorHz = 59.0;

    private const double AspectTolerance = 0.05;

    public static DisplayMode? PickBestMode(IEnumerable<DisplayMode>? availableModes, DisplayInfo display)
    {
        ArgumentNullException.ThrowIfNull(display);
        if (availableModes == null)
            return null;

        var candidates = availableModes
            .Where(m => !m.IsInterlaced && m.Width > 0 && m.Height > 0)
            .ToList();

        if (display.Connection is ConnectionType.Vga or ConnectionType.WiFi)
        {
            candidates = candidates.Where(m => m.Width <= 1920 && m.Height <= 1080 && m.RefreshHz <= 60.01).ToList();
        }

        // Adapters and TVs advertise modes the panel can't show (a VGA adapter offering 1080p to a 1366x768
        // monitor gives "Input not supported"), so never go above the monitor's own native resolution.
        var native = display.NativeMode is { Width: > 0, Height: > 0 } n ? n : null;
        if (native != null)
        {
            var withinNative = candidates.Where(m => m.Width <= native.Width && m.Height <= native.Height).ToList();
            if (withinNative.Count > 0)
            {
                candidates = withinNative;
            }
        }

        // A TV that only reaches 4K at 24-30 Hz feels laggy on a desktop; prefer its best 60 Hz mode.
        var smooth = candidates.Where(m => m.RefreshHz >= SmoothRefreshFloorHz).ToList();
        if (smooth.Count > 0)
        {
            candidates = smooth;
        }

        // The native resolution is the sharpest picture, so use it whenever it is available at a smooth rate.
        if (native != null)
        {
            var atNative = candidates.Where(m => m.Width == native.Width && m.Height == native.Height).ToList();
            if (atNative.Count > 0)
            {
                return atNative.OrderByDescending(m => m.RefreshHz).First();
            }
        }

        if (display.PhysicalWidthMm > 0 && display.PhysicalHeightMm > 0)
        {
            double panelAspect = display.PhysicalWidthMm / display.PhysicalHeightMm;
            var matchingAspect = candidates
                .Where(m => Math.Abs((double)m.Width / m.Height - panelAspect) / panelAspect <= AspectTolerance)
                .ToList();
            if (matchingAspect.Count > 0)
            {
                candidates = matchingAspect;
            }
        }

        return candidates
            .OrderByDescending(m => (long)m.Width * m.Height)
            .ThenByDescending(m => m.RefreshHz)
            .FirstOrDefault();
    }
}
