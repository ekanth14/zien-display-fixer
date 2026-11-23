namespace ZienDisplayFixer.App.Services.Linux;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public class LinuxWirelessDisplayLauncher : IWirelessDisplayLauncher
{
    private readonly Func<bool>? _supportedChecker;
    private readonly Action<string>? _processRunner;

    public LinuxWirelessDisplayLauncher(Func<bool>? supportedChecker = null, Action<string>? processRunner = null)
    {
        _supportedChecker = supportedChecker;
        _processRunner = processRunner;
    }

    public bool IsSupported => _supportedChecker != null ? _supportedChecker() : CheckIsSupported();

    public Task OpenWirelessDisplayPickerAsync(CancellationToken ct = default)
    {
        if (!IsSupported)
        {
            return Task.CompletedTask;
        }

        if (_processRunner != null)
        {
            _processRunner("gnome-network-displays");
            return Task.CompletedTask;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "gnome-network-displays",
                UseShellExecute = true
            });
        }
        catch
        {
            // Headless or process launch failure
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WirelessDisplayDevice>> ScanNearbyAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<WirelessDisplayDevice>>([]);
    }

    private static bool CheckIsSupported()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return false;
        }

        if (File.Exists("/usr/bin/gnome-network-displays") || File.Exists("/usr/local/bin/gnome-network-displays"))
        {
            return true;
        }

        return CommandExists("gnome-network-displays");
    }

    private static bool CommandExists(string command)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            if (!process.WaitForExit(1000))
            {
                try { process.Kill(); } catch { }
                return false;
            }
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
