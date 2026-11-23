namespace ZienDisplayFixer.App.Services.MacOS;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ZienDisplayFixer.Core.Interfaces;

public class MacEdidReader : IEdidReader
{
    private static readonly Regex EdidRegex = new(
        @"\""IODisplayEDID\""\s*=\s*<([0-9a-fA-F\s]+)>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<byte[]?> ReadRawEdidAsync(string displayId, CancellationToken ct = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
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

            // If there is only one EDID and displayId is "1", "0", "default", or primary
            if (edids.Count == 1)
            {
                foreach (var kvp in edids)
                {
                    return kvp.Value;
                }
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

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return result;
        }

        try
        {
            string output = await RunIORegAsync("-c AppleDisplay", ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(output))
            {
                // Fallback for built-in displays that register under AppleBacklightDisplay
                output = await RunIORegAsync("-c AppleBacklightDisplay", ct).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                return result;
            }

            var matches = EdidRegex.Matches(output);
            int index = 0;
            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    string hex = match.Groups[1].Value.Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "");
                    if (hex.Length >= 256) // 128 bytes * 2 hex chars
                    {
                        byte[] edidBytes = ConvertHexToBytes(hex);
                        if (edidBytes.Length >= 128)
                        {
                            result[$"AppleDisplay-{index}"] = edidBytes;
                            result[$"{index}"] = edidBytes;
                            result[$"{index + 1}"] = edidBytes; // 1-based display index
                            index++;
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore execution failures
        }

        return result;
    }

    private static async Task<string> RunIORegAsync(string classArg, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ioreg",
                Arguments = $"-l -w0 -r {classArg}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            if (!process.Start())
            {
                return string.Empty;
            }

            string stdout = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return stdout;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static byte[] ConvertHexToBytes(string hex)
    {
        int length = hex.Length / 2;
        var bytes = new byte[length];
        for (int i = 0; i < length; i++)
        {
            bytes[i] = byte.Parse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        return bytes;
    }
}
