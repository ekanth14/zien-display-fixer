namespace ZienDisplayFixer.App.Services.Windows;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public class WindowsWirelessDisplayLauncher : IWirelessDisplayLauncher
{
    private readonly Action<string>? _processRunner;

    public WindowsWirelessDisplayLauncher(Action<string>? processRunner = null)
    {
        _processRunner = processRunner;
    }

    public bool IsSupported => true;

    public Task OpenWirelessDisplayPickerAsync(CancellationToken ct = default)
    {
        if (_processRunner != null)
        {
            _processRunner("ms-settings-connectabledevices:devicediscovery");
            return Task.CompletedTask;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings-connectabledevices:devicediscovery",
                UseShellExecute = true
            });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:display",
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
        IReadOnlyList<WirelessDisplayDevice> devices =
        [
            new WirelessDisplayDevice("w-living-room", "Living room TV", false, 55.0),
            new WirelessDisplayDevice("w-bedroom", "Bedroom TV", false, 43.0),
            new WirelessDisplayDevice("w-office", "Office display", false, 27.0)
        ];

        return Task.FromResult(devices);
    }
}
