namespace ZienDisplayFixer.Core.Interfaces;

public interface IWallpaperService
{
    Task<string?> GetCurrentWallpaperPathAsync(CancellationToken ct = default);
    Task<Stream?> GetWallpaperImageStreamAsync(CancellationToken ct = default);
    event EventHandler? WallpaperChanged;
}
