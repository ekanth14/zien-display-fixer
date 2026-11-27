namespace ZienDisplayFixer.Core.Interfaces;

using ZienDisplayFixer.Core.Models;

public interface IDisplayService
{
    Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DisplayMode>> GetModesAsync(string displayId, CancellationToken ct = default);
    Task<bool> TestModeAsync(string displayId, DisplayMode mode, CancellationToken ct = default);
    Task<bool> ApplyModeAsync(string displayId, DisplayMode mode, CancellationToken ct = default);
    Task<bool> RestorePreviousModesAsync(CancellationToken ct = default);

    // Platforms without position support fall back to applying the changed modes one by one.
    async Task<bool> ApplyConfigurationAsync(IReadOnlyList<DisplayTarget> targets, CancellationToken ct = default)
    {
        foreach (var target in targets.Where(t => t.ModeChanged))
        {
            if (!await ApplyModeAsync(target.DisplayId, target.Mode, ct).ConfigureAwait(false))
            {
                return false;
            }
        }
        return true;
    }

    event EventHandler? DisplaysChanged;
}
