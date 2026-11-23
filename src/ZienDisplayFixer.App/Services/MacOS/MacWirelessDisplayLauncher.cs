namespace ZienDisplayFixer.App.Services.MacOS;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public class MacWirelessDisplayLauncher : IWirelessDisplayLauncher
{
    private readonly Action<string, string>? _processRunner;

    public MacWirelessDisplayLauncher(Action<string, string>? processRunner = null)
    {
        _processRunner = processRunner;
    }

    public bool IsSupported => true;

    public Task OpenWirelessDisplayPickerAsync(CancellationToken ct = default)
    {
        if (_processRunner != null)
        {
            _processRunner("open", "x-apple.systempreferences:com.apple.Displays-Settings.extension");
            return Task.CompletedTask;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                Arguments = "x-apple.systempreferences:com.apple.Displays-Settings.extension",
                UseShellExecute = true
            });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = "/System/Library/PreferencePanes/Displays.prefPane",
                    UseShellExecute = true
                });
            }
            catch
            {
                // Headless or permission failure
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WirelessDisplayDevice>> ScanNearbyAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<WirelessDisplayDevice>>([]);
    }
}
