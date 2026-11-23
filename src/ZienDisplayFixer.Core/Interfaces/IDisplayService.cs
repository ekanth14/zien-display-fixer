namespace ZienDisplayFixer.Core.Interfaces;

using ZienDisplayFixer.Core.Models;

public interface IDisplayService
{
    Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DisplayMode>> GetModesAsync(string displayId, CancellationToken ct = default);
    Task<bool> TestModeAsync(string displayId, DisplayMode mode, CancellationToken ct = default);
    Task<bool> ApplyModeAsync(string displayId, DisplayMode mode, CancellationToken ct = default);
    Task<bool> RestorePreviousModesAsync(CancellationToken ct = default);
    event EventHandler? DisplaysChanged;
}
