namespace ZienDisplayFixer.Core.Edid;

using System;
using System.Text;
using ZienDisplayFixer.Core.Models;

public static class EdidParser
{
    private static readonly byte[] ExpectedHeader = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

    private static readonly (string Name, double Ratio)[] StandardAspectRatios =
    [
        ("16:9", 16.0 / 9.0),
        ("16:10", 16.0 / 10.0),
        ("21:9", 21.0 / 9.0),
        ("32:9", 32.0 / 9.0),
        ("4:3", 4.0 / 3.0),
        ("5:4", 5.0 / 4.0)
    ];

    public static EdidData? Parse(byte[]? rawEdid)
    {
        if (rawEdid == null || rawEdid.Length < 128)
        {
            return null;
        }

        // Header check: bytes 0..7 must match 00 FF FF FF FF FF FF 00
        for (int i = 0; i < 8; i++)
        {
            if (rawEdid[i] != ExpectedHeader[i])
            {
                return null;
            }
        }

        // Checksum check: sum of first 128 bytes modulo 256 == 0
        int sum = 0;
        for (int i = 0; i < 128; i++)
        {
            sum += rawEdid[i];
        }

        if ((sum % 256) != 0)
        {
            return null;
        }

        // 3-letter compressed ASCII vendor decoding from bytes 8..9
        byte b8 = rawEdid[8];
        byte b9 = rawEdid[9];
        int c1 = (b8 & 0x7C) >> 2;
        int c2 = ((b8 & 0x03) << 3) | ((b9 & 0xE0) >> 5);
        int c3 = b9 & 0x1F;

        char char1 = c1 is >= 1 and <= 26 ? (char)('A' + c1 - 1) : '?';
        char char2 = c2 is >= 1 and <= 26 ? (char)('A' + c2 - 1) : '?';
        char char3 = c3 is >= 1 and <= 26 ? (char)('A' + c3 - 1) : '?';
        string manufacturerId = $"{char1}{char2}{char3}";

        // Physical dimensions: byte 21 (width cm), byte 22 (height cm)
        int widthCm = rawEdid[21];
        int heightCm = rawEdid[22];
        double diagonalInches = 0.0;
        string aspectRatio = string.Empty;

        if (widthCm > 0 && heightCm > 0)
        {
            diagonalInches = Math.Round(Math.Sqrt((double)widthCm * widthCm + (double)heightCm * heightCm) / 2.54, 1, MidpointRounding.AwayFromZero);
            double ratio = (double)widthCm / heightCm;
            aspectRatio = GetClosestAspectRatio(ratio);
        }

        // Four 18-byte Detailed Timing Descriptor (DTD) blocks starting at offset 54
        // Block 0 (54..71): Preferred native mode
        DisplayMode? preferredMode = null;
        int pixelClock10Khz = rawEdid[54] | (rawEdid[55] << 8);
        if (pixelClock10Khz > 0)
        {
            long pixelClockHz = (long)pixelClock10Khz * 10000;
            int hActive = rawEdid[56] | ((rawEdid[58] & 0xF0) << 4);
            int hBlank = rawEdid[57] | ((rawEdid[58] & 0x0F) << 8);
            int vActive = rawEdid[59] | ((rawEdid[61] & 0xF0) << 4);
            int vBlank = rawEdid[60] | ((rawEdid[61] & 0x0F) << 8);
            int hTotal = hActive + hBlank;
            int vTotal = vActive + vBlank;
            double refreshHz = (hTotal > 0 && vTotal > 0)
                ? Math.Round((double)pixelClockHz / (hTotal * vTotal), 2, MidpointRounding.AwayFromZero)
                : 60.0;
            bool isInterlaced = (rawEdid[71] & 0x80) != 0;

            preferredMode = new DisplayMode(hActive, vActive, refreshHz, isInterlaced);
        }

        // Blocks 0..3: Check for Monitor Name (0xFC) and Serial (0xFF) descriptors
        string? modelName = null;
        string? serialNumber = null;

        for (int block = 0; block < 4; block++)
        {
            int offset = 54 + (block * 18);
            if (rawEdid[offset] == 0 && rawEdid[offset + 1] == 0 && rawEdid[offset + 2] == 0)
            {
                byte descriptorType = rawEdid[offset + 3];
                if (descriptorType == 0xFC && modelName == null)
                {
                    modelName = ExtractDescriptorString(rawEdid, offset + 5, 13);
                }
                else if (descriptorType == 0xFF && serialNumber == null)
                {
                    serialNumber = ExtractDescriptorString(rawEdid, offset + 5, 13);
                }
            }
        }

        return new EdidData(
            manufacturerId: manufacturerId,
            modelName: modelName,
            serialNumber: serialNumber,
            physicalWidthCm: widthCm,
            physicalHeightCm: heightCm,
            diagonalInches: diagonalInches,
            aspectRatio: aspectRatio,
            preferredMode: preferredMode
        );
    }

    private static string GetClosestAspectRatio(double ratio)
    {
        string closestName = StandardAspectRatios[0].Name;
        double minDiff = Math.Abs(ratio - StandardAspectRatios[0].Ratio);

        for (int i = 1; i < StandardAspectRatios.Length; i++)
        {
            double diff = Math.Abs(ratio - StandardAspectRatios[i].Ratio);
            if (diff < minDiff)
            {
                minDiff = diff;
                closestName = StandardAspectRatios[i].Name;
            }
        }

        return closestName;
    }

    private static string? ExtractDescriptorString(byte[] data, int startOffset, int length)
    {
        int validLen = 0;
        for (int i = 0; i < length && (startOffset + i) < data.Length; i++)
        {
            byte b = data[startOffset + i];
            if (b == 0x0A || b == 0x00)
            {
                break;
            }
            validLen++;
        }

        if (validLen == 0)
        {
            return null;
        }

        string text = Encoding.ASCII.GetString(data, startOffset, validLen).Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
