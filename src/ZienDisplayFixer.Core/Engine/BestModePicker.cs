namespace ZienDisplayFixer.Core.Engine;

using ZienDisplayFixer.Core.Models;

public static class BestModePicker
{
    public static DisplayMode? PickBestMode(IEnumerable<DisplayMode>? availableModes, DisplayInfo display)
    {
        if (availableModes == null)
            return null;

        var candidates = availableModes.Where(m => !m.IsInterlaced);

        if (display.Connection is ConnectionType.Vga or ConnectionType.WiFi)
        {
            candidates = candidates.Where(m => m.Width <= 1920 && m.Height <= 1080 && m.RefreshHz <= 60.01);
        }

        return candidates
            .OrderByDescending(m => (long)m.Width * m.Height)
            .ThenByDescending(m => m.RefreshHz)
            .FirstOrDefault();
    }
}
