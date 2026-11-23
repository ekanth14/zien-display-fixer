namespace ZienDisplayFixer.App.Services.Linux;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ZienDisplayFixer.Core.Interfaces;

public class LinuxWallpaperService : IWallpaperService
{
    public event EventHandler? WallpaperChanged;

    public async Task<string?> GetCurrentWallpaperPathAsync(CancellationToken ct = default)
    {
        try
        {
            string? uri = await RunGsettingsAsync("picture-uri", ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(uri) || uri == "''" || uri == "\"\"")
            {
                uri = await RunGsettingsAsync("picture-uri-dark", ct).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(uri))
            {
                string clean = uri.Trim().Trim('\'', '"');
                if (clean.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean[7..];
                }
                clean = Uri.UnescapeDataString(clean);

                if (File.Exists(clean))
                {
                    return clean;
                }
            }
        }
        catch
        {
            // Ignore execution errors on non-Linux or headless
        }

        return null;
    }

    private static async Task<string?> RunGsettingsAsync(string key, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        Process? process = null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "gsettings",
                Arguments = $"get org.gnome.desktop.background {key}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process = Process.Start(psi);
            if (process == null) return null;

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            string output = await stdoutTask.ConfigureAwait(false);
            _ = await stderrTask.ConfigureAwait(false);

            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch (OperationCanceledException)
        {
            try { if (process is { HasExited: false }) process.Kill(entireProcessTree: true); } catch { }
            return null;
        }
        catch
        {
            try { if (process is { HasExited: false }) process.Kill(entireProcessTree: true); } catch { }
            return null;
        }
        finally
        {
            process?.Dispose();
        }
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

