namespace ZienDisplayFixer.App.Services.MacOS;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ZienDisplayFixer.Core.Interfaces;

public class MacWallpaperService : IWallpaperService
{
    public event EventHandler? WallpaperChanged;

    public async Task<string?> GetCurrentWallpaperPathAsync(CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        Process? process = null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = "-e 'tell app \"finder\" to get posix path of (get desktop picture as alias)'",
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

            if (process.ExitCode == 0)
            {
                string path = output.Trim().Trim('\'', '"');
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    return path;
                }
            }
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

        return null;
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

