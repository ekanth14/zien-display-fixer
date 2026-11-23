namespace ZienDisplayFixer.App.Services.Windows;

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZienDisplayFixer.App.Services.Windows.Native;
using ZienDisplayFixer.Core.Interfaces;

public class WindowsWallpaperService : IWallpaperService
{
    public event EventHandler? WallpaperChanged;

    public Task<string?> GetCurrentWallpaperPathAsync(CancellationToken ct = default)
    {
        try
        {
            var sb = new StringBuilder(512);
            if (User32.SystemParametersInfo(User32.SPI_GETDESKWALLPAPER, 512, sb, 0))
            {
                string path = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    return Task.FromResult<string?>(path);
                }
            }

            // Fallback: check Windows default transcoded wallpaper location
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string transcoded = Path.Combine(appData, "Microsoft", "Windows", "Themes", "TranscodedWallpaper");
            if (File.Exists(transcoded))
            {
                return Task.FromResult<string?>(transcoded);
            }
        }
        catch
        {
            // Ignore wallpaper read errors
        }

        return Task.FromResult<string?>(null);
    }

    public async Task<Stream?> GetWallpaperImageStreamAsync(CancellationToken ct = default)
    {
        string? path = await GetCurrentWallpaperPathAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
            return new MemoryStream(bytes);
        }
        catch
        {
            return null;
        }
    }

    protected virtual void OnWallpaperChanged()
    {
        WallpaperChanged?.Invoke(this, EventArgs.Empty);
    }
}

