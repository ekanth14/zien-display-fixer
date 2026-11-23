namespace ZienDisplayFixer.Core.Interfaces;

using ZienDisplayFixer.Core.Models;

public interface IWirelessDisplayLauncher
{
    bool IsSupported { get; }
    Task OpenWirelessDisplayPickerAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WirelessDisplayDevice>> ScanNearbyAsync(CancellationToken ct = default);
}
