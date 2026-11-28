namespace ZienDisplayFixer.App.Services.Windows;

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using ZienDisplayFixer.App.Services.Windows.Native;

public static class WindowsMonitorEdid
{
    // Reads the EDID Windows actually uses for the monitor on an attached adapter output (e.g. \\.\DISPLAY2).
    // The exact PnP instance is resolved because Windows keeps stale entries for every port a model was ever
    // plugged into, and an EDID_OVERRIDE (written by tools like CRU) takes precedence over the monitor's own EDID.
    [SupportedOSPlatform("windows")]
    public static byte[]? ReadForAdapter(string adapterDeviceName)
    {
        var monitor = new DISPLAY_DEVICEW();
        monitor.cb = (uint)Marshal.SizeOf<DISPLAY_DEVICEW>();
        if (!User32.EnumDisplayDevices(adapterDeviceName, 0, ref monitor, User32.EDD_GET_DEVICE_INTERFACE_NAME))
        {
            return null;
        }

        string? instancePath = InstancePathFromInterfaceName(monitor.DeviceID);
        return instancePath == null ? null : ReadForInstance(instancePath);
    }

    [SupportedOSPlatform("windows")]
    public static byte[]? ReadForInstance(string instancePath)
    {
        try
        {
            using var parameters = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\{instancePath}\Device Parameters");
            if (parameters == null)
            {
                return null;
            }

            byte[]? edid = parameters.GetValue("EDID") as byte[];
            using var overrideKey = parameters.OpenSubKey("EDID_OVERRIDE");
            if (overrideKey?.GetValue("0") is byte[] { Length: 128 } overrideBlock0)
            {
                if (edid is { Length: >= 128 })
                {
                    var merged = (byte[])edid.Clone();
                    Array.Copy(overrideBlock0, merged, 128);
                    return merged;
                }
                return overrideBlock0;
            }

            return edid is { Length: >= 128 } ? edid : null;
        }
        catch
        {
            return null;
        }
    }

    // "\\?\DISPLAY#ACR0358#4&1c5b15a4&0&UID24647#{e6f07b5f-...}" -> "DISPLAY\ACR0358\4&1c5b15a4&0&UID24647"
    public static string? InstancePathFromInterfaceName(string? interfaceName)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            return null;
        }

        string trimmed = interfaceName.StartsWith(@"\\?\", StringComparison.Ordinal) ? interfaceName[4..] : interfaceName;
        var parts = trimmed.Split('#');
        if (parts.Length < 3 || !parts[0].Equals("DISPLAY", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $@"{parts[0]}\{parts[1]}\{parts[2]}";
    }
}
