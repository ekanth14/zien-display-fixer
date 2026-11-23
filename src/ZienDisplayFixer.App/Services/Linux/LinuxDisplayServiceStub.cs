namespace ZienDisplayFixer.App.Services.Linux;

using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public class LinuxDisplayServiceStub : IDisplayService
{
    public event EventHandler? DisplaysChanged;

    public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default) =>
        throw new NotSupportedException("LinuxDisplayService is not yet implemented.");

    public Task<IReadOnlyList<DisplayMode>> GetModesAsync(string displayId, CancellationToken ct = default) =>
        throw new NotSupportedException("LinuxDisplayService is not yet implemented.");

    public Task<bool> TestModeAsync(string displayId, DisplayMode mode, CancellationToken ct = default) =>
        throw new NotSupportedException("LinuxDisplayService is not yet implemented.");

    public Task<bool> ApplyModeAsync(string displayId, DisplayMode mode, CancellationToken ct = default) =>
        throw new NotSupportedException("LinuxDisplayService is not yet implemented.");

    public Task<bool> RestorePreviousModesAsync(CancellationToken ct = default) =>
        throw new NotSupportedException("LinuxDisplayService is not yet implemented.");
}
