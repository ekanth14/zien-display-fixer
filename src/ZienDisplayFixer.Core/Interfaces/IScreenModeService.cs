namespace ZienDisplayFixer.Core.Interfaces;

using ZienDisplayFixer.Core.Models;

public interface IScreenModeService
{
    Task<ScreenTopology> GetCurrentTopologyAsync(CancellationToken ct = default);
    Task SetTopologyAsync(ScreenTopology topology, CancellationToken ct = default);
}
