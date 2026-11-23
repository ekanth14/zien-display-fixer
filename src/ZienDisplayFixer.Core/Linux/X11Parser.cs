namespace ZienDisplayFixer.Core.Linux;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ZienDisplayFixer.Core.Models;

public static class X11Parser
{
    private static readonly Regex DisplayHeaderRegex = new(
        @"^(?<name>[A-Za-z0-9\-_]+)\s+connected\s*(?<primary>primary\s*)?(?:(?<w>\d+)x(?<h>\d+)(?<x>[+-]\d+)(?<y>[+-]\d+))?(?:[^\d\n\r]*(?:(?<physW>\d+)mm\s*x\s*(?<physH>\d+)mm)?)?",
        RegexOptions.Compiled);

    private static readonly Regex ModeLineRegex = new(
        @"^\s+(?<w>\d+)x(?<h>\d+)(?<resInterlaced>i)?\s+(?<rates>.+)$",
        RegexOptions.Compiled);

    private static readonly Regex RateTokenRegex = new(
        @"^(?<rate>\d+(?:\.\d+)?)(?<rateInterlaced>i)?(?<flags>[\*\+]*)$",
        RegexOptions.Compiled);

    public static IReadOnlyList<DisplayInfo> ParseXRandrQuery(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<DisplayInfo>();
        }

        var displays = new List<DisplayInfo>();
        var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        int i = 0;
        while (i < lines.Length)
        {
            string line = lines[i];

            // Connected display lines start with the port name without leading whitespace
            if (!line.StartsWith(" ") && !line.StartsWith("\t"))
            {
                var match = DisplayHeaderRegex.Match(line);
                if (match.Success)
                {
                    string name = match.Groups["name"].Value;
                    bool isPrimary = match.Groups["primary"].Success;

                    bool hasResolution = match.Groups["w"].Success && match.Groups["h"].Success;
                    int width = hasResolution ? int.Parse(match.Groups["w"].Value, CultureInfo.InvariantCulture) : 0;
                    int height = hasResolution ? int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture) : 0;
                    int posX = match.Groups["x"].Success ? int.Parse(match.Groups["x"].Value, CultureInfo.InvariantCulture) : 0;
                    int posY = match.Groups["y"].Success ? int.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture) : 0;

                    double physW = match.Groups["physW"].Success ? double.Parse(match.Groups["physW"].Value, CultureInfo.InvariantCulture) : 0.0;
                    double physH = match.Groups["physH"].Success ? double.Parse(match.Groups["physH"].Value, CultureInfo.InvariantCulture) : 0.0;

                    ConnectionType connType = DetectConnectionType(name);

                    // Scan child mode lines for current active mode (indicated by '*')
                    DisplayMode? currentMode = null;
                    double activeRate = 60.0;
                    bool activeInterlaced = false;
                    bool foundActiveRate = false;

                    i++;
                    while (i < lines.Length && (lines[i].StartsWith(" ") || lines[i].StartsWith("\t")))
                    {
                        var modeMatch = ModeLineRegex.Match(lines[i]);
                        if (modeMatch.Success)
                        {
                            int mw = int.Parse(modeMatch.Groups["w"].Value, CultureInfo.InvariantCulture);
                            int mh = int.Parse(modeMatch.Groups["h"].Value, CultureInfo.InvariantCulture);
                            bool resInterlaced = modeMatch.Groups["resInterlaced"].Success;

                            string ratesPart = modeMatch.Groups["rates"].Value;
                            var rateTokens = ratesPart.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                            foreach (var token in rateTokens)
                            {
                                var rateMatch = RateTokenRegex.Match(token);
                                if (rateMatch.Success)
                                {
                                    string flags = rateMatch.Groups["flags"].Value;
                                    if (flags.Contains('*'))
                                    {
                                        activeRate = double.Parse(rateMatch.Groups["rate"].Value, CultureInfo.InvariantCulture);
                                        activeInterlaced = resInterlaced || rateMatch.Groups["rateInterlaced"].Success;
                                        foundActiveRate = true;
                                        break;
                                    }
                                }
                            }
                        }
                        i++;
                    }

                    if (hasResolution)
                    {
                        currentMode = new DisplayMode(width, height, foundActiveRate ? activeRate : 60.0, activeInterlaced);
                    }

                    var info = new DisplayInfo(
                        id: name,
                        name: name,
                        model: name,
                        isPrimary: isPrimary,
                        positionX: posX,
                        positionY: posY,
                        currentMode: currentMode,
                        physicalWidthMm: physW,
                        physicalHeightMm: physH,
                        connection: connType
                    );

                    displays.Add(info);
                    continue;
                }
            }

            i++;
        }

        return displays;
    }

    public static IReadOnlyList<DisplayMode> ParseXRandrModes(string? output, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(displayName))
        {
            return Array.Empty<DisplayMode>();
        }

        var modes = new List<DisplayMode>();
        var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        int i = 0;
        bool inTargetDisplay = false;

        while (i < lines.Length)
        {
            string line = lines[i];

            if (!line.StartsWith(" ") && !line.StartsWith("\t"))
            {
                var match = DisplayHeaderRegex.Match(line);
                if (match.Success && string.Equals(match.Groups["name"].Value, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    inTargetDisplay = true;
                }
                else
                {
                    inTargetDisplay = false;
                }
            }
            else if (inTargetDisplay)
            {
                var modeMatch = ModeLineRegex.Match(line);
                if (modeMatch.Success)
                {
                    int width = int.Parse(modeMatch.Groups["w"].Value, CultureInfo.InvariantCulture);
                    int height = int.Parse(modeMatch.Groups["h"].Value, CultureInfo.InvariantCulture);
                    bool resInterlaced = modeMatch.Groups["resInterlaced"].Success;

                    string ratesPart = modeMatch.Groups["rates"].Value;
                    var rateTokens = ratesPart.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var token in rateTokens)
                    {
                        var rateMatch = RateTokenRegex.Match(token);
                        if (rateMatch.Success)
                        {
                            double rate = double.Parse(rateMatch.Groups["rate"].Value, CultureInfo.InvariantCulture);
                            bool isInterlaced = resInterlaced || rateMatch.Groups["rateInterlaced"].Success;

                            var mode = new DisplayMode(width, height, rate, isInterlaced);
                            if (!modes.Contains(mode))
                            {
                                modes.Add(mode);
                            }
                        }
                    }
                }
            }

            i++;
        }

        return modes;
    }

    private static ConnectionType DetectConnectionType(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return ConnectionType.Unknown;
        }

        if (portName.StartsWith("HDMI", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectionType.Hdmi;
        }

        if (portName.StartsWith("eDP", StringComparison.OrdinalIgnoreCase) ||
            portName.StartsWith("DP", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectionType.DisplayPort;
        }

        if (portName.StartsWith("VGA", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectionType.Vga;
        }

        if (portName.StartsWith("USB", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectionType.UsbC;
        }

        return ConnectionType.Unknown;
    }
}
