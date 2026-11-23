namespace ZienDisplayFixer.App.Services.Windows;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using ZienDisplayFixer.App.Services.Windows.Native;
using ZienDisplayFixer.Core.Interfaces;

public class WindowsEdidReader : IEdidReader
{
    private const string DisplayEnumRegistryKey = @"SYSTEM\CurrentControlSet\Enum\DISPLAY";

    public Task<byte[]?> ReadRawEdidAsync(string displayId, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult<byte[]?>(null);
        }

        try
        {
            var edids = ReadAllEdidsInternal();

            if (edids.TryGetValue(displayId, out var exactMatch))
            {
                return Task.FromResult<byte[]?>(exactMatch);
            }

            // Case-insensitive check
            foreach (var kvp in edids)
            {
                if (string.Equals(kvp.Key, displayId, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult<byte[]?>(kvp.Value);
                }
            }

            // If displayId is e.g. \\.\DISPLAY1, resolve monitor hardware ID using User32
            if (displayId.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase) ||
                displayId.StartsWith("DISPLAY", StringComparison.OrdinalIgnoreCase))
            {
                var monitorDevice = new DISPLAY_DEVICEW();
                monitorDevice.cb = (uint)Marshal.SizeOf<DISPLAY_DEVICEW>();

                if (User32.EnumDisplayDevices(displayId, 0, ref monitorDevice, 0))
                {
                    if (edids.TryGetValue(monitorDevice.DeviceID, out var devIdMatch))
                    {
                        return Task.FromResult<byte[]?>(devIdMatch);
                    }

                    // Try matching partial key from monitorDevice.DeviceID (e.g. SDC41AA or 4&1c5b...)
                    foreach (var kvp in edids)
                    {
                        if (!string.IsNullOrEmpty(kvp.Key) &&
                            (monitorDevice.DeviceID.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                             kvp.Key.Contains(monitorDevice.DeviceID, StringComparison.OrdinalIgnoreCase)))
                        {
                            return Task.FromResult<byte[]?>(kvp.Value);
                        }
                    }
                }
            }

            // Partial match fallback
            foreach (var kvp in edids)
            {
                if (!string.IsNullOrEmpty(kvp.Key) &&
                    (displayId.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                     kvp.Key.Contains(displayId, StringComparison.OrdinalIgnoreCase)))
                {
                    return Task.FromResult<byte[]?>(kvp.Value);
                }
            }

            return Task.FromResult<byte[]?>(null);
        }
        catch
        {
            return Task.FromResult<byte[]?>(null);
        }
    }

    public Task<IReadOnlyDictionary<string, byte[]>> ReadAllEdidsAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult<IReadOnlyDictionary<string, byte[]>>(new Dictionary<string, byte[]>());
        }

        try
        {
            var dict = ReadAllEdidsInternal();
            return Task.FromResult<IReadOnlyDictionary<string, byte[]>>(dict);
        }
        catch
        {
            return Task.FromResult<IReadOnlyDictionary<string, byte[]>>(new Dictionary<string, byte[]>());
        }
    }

    [SupportedOSPlatform("windows")]
    private static Dictionary<string, byte[]> ReadAllEdidsInternal()
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var displayKey = Registry.LocalMachine.OpenSubKey(DisplayEnumRegistryKey);
            if (displayKey == null)
            {
                return result;
            }

            foreach (string monitorName in displayKey.GetSubKeyNames())
            {
                using var monitorKey = displayKey.OpenSubKey(monitorName);
                if (monitorKey == null) continue;

                foreach (string instanceName in monitorKey.GetSubKeyNames())
                {
                    using var instanceKey = monitorKey.OpenSubKey(instanceName);
                    if (instanceKey == null) continue;

                    using var devParamsKey = instanceKey.OpenSubKey("Device Parameters");
                    if (devParamsKey?.GetValue("EDID") is byte[] edid && edid.Length >= 128)
                    {
                        result[instanceName] = edid;
                        result[monitorName] = edid;
                        result[$@"DISPLAY\{monitorName}\{instanceName}"] = edid;
                    }
                }
            }

            // Enumerate active desktop adapters and map their display IDs (\\.\DISPLAY1)
            uint devNum = 0;
            while (true)
            {
                var adapterDevice = new DISPLAY_DEVICEW();
                adapterDevice.cb = (uint)Marshal.SizeOf<DISPLAY_DEVICEW>();

                if (!User32.EnumDisplayDevices(null, devNum, ref adapterDevice, 0))
                {
                    break;
                }

                if ((adapterDevice.StateFlags & User32.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0)
                {
                    var monitorDevice = new DISPLAY_DEVICEW();
                    monitorDevice.cb = (uint)Marshal.SizeOf<DISPLAY_DEVICEW>();

                    if (User32.EnumDisplayDevices(adapterDevice.DeviceName, 0, ref monitorDevice, 0))
                    {
                        byte[]? matchingEdid = null;
                        foreach (var kvp in result)
                        {
                            if (!string.IsNullOrEmpty(kvp.Key) &&
                                monitorDevice.DeviceID.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                            {
                                matchingEdid = kvp.Value;
                                break;
                            }
                        }

                        if (matchingEdid != null)
                        {
                            result[adapterDevice.DeviceName] = matchingEdid;
                            if (!string.IsNullOrEmpty(monitorDevice.DeviceName))
                            {
                                result[monitorDevice.DeviceName] = matchingEdid;
                            }
                        }
                    }
                }

                devNum++;
            }
        }
        catch
        {
            // Suppress registry access errors
        }

        return result;
    }
}
