namespace ZienDisplayFixer.App.Services.Linux;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ZienDisplayFixer.Core.Interfaces;

public class LinuxEdidReader : IEdidReader
{
    private const string DrmPath = "/sys/class/drm";

    public async Task<byte[]?> ReadRawEdidAsync(string displayId, CancellationToken ct = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return null;
        }

        try
        {
            var edids = await ReadAllEdidsAsync(ct).ConfigureAwait(false);

            if (edids.TryGetValue(displayId, out var edid))
            {
                return edid;
            }

            foreach (var kvp in edids)
            {
                if (string.Equals(kvp.Key, displayId, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            // Try matching normalized connector name (e.g. HDMI-A-1 vs HDMI-1)
            string normalized = displayId.Replace("-A-", "-");
            if (edids.TryGetValue(normalized, out var normEdid))
            {
                return normEdid;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyDictionary<string, byte[]>> ReadAllEdidsAsync(CancellationToken ct = default)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return result;
        }

        try
        {
            if (!Directory.Exists(DrmPath))
            {
                return result;
            }

            var subDirs = Directory.GetDirectories(DrmPath);
            foreach (var dir in subDirs)
            {
                string edidFile = Path.Combine(dir, "edid");
                if (File.Exists(edidFile))
                {
                    try
                    {
                        byte[] bytes = await File.ReadAllBytesAsync(edidFile, ct).ConfigureAwait(false);
                        if (bytes.Length >= 128)
                        {
                            string dirName = Path.GetFileName(dir);
                            result[dirName] = bytes;

                            int dashIndex = dirName.IndexOf('-');
                            if (dashIndex >= 0 && dashIndex < dirName.Length - 1)
                            {
                                string portName = dirName.Substring(dashIndex + 1);
                                result[portName] = bytes;

                                string normalizedPort = portName.Replace("-A-", "-");
                                if (!result.ContainsKey(normalizedPort))
                                {
                                    result[normalizedPort] = bytes;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore unreadable edid files
                    }
                }
            }
        }
        catch
        {
            // Ignore sysfs navigation failures
        }

        return result;
    }
}
