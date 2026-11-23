namespace ZienDisplayFixer.Tests.Edid;

using System;
using System.Text;
using Xunit;
using ZienDisplayFixer.Core.Edid;
using ZienDisplayFixer.Core.Models;

public class EdidParserTests
{
    private static readonly byte[] ValidHeader = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

    private static byte[] CreateEdid(
        string vendor = "DEL",
        int widthCm = 60,
        int heightCm = 34,
        int hActive = 2560,
        int vActive = 1440,
        int hBlank = 160,
        int vBlank = 41,
        int pixelClock10Khz = 58009,
        bool isInterlaced = false,
        string? modelName = "DELL U2720Q",
        string? serialNumber = "CN-012345")
    {
        var edid = new byte[128];

        // 1. Header (bytes 0..7)
        Array.Copy(ValidHeader, 0, edid, 0, 8);

        // 2. Vendor ID (bytes 8..9) - 3 letters compressed ASCII
        if (vendor.Length == 3)
        {
            int c1 = char.ToUpperInvariant(vendor[0]) - 'A' + 1;
            int c2 = char.ToUpperInvariant(vendor[1]) - 'A' + 1;
            int c3 = char.ToUpperInvariant(vendor[2]) - 'A' + 1;

            edid[8] = (byte)(((c1 & 0x1F) << 2) | ((c2 >> 3) & 0x03));
            edid[9] = (byte)(((c2 & 0x07) << 5) | (c3 & 0x1F));
        }

        // Product code (bytes 10..11)
        edid[10] = 0x20;
        edid[11] = 0x27;

        // Serial number (bytes 12..15)
        edid[12] = 0x01;
        edid[13] = 0x00;
        edid[14] = 0x00;
        edid[15] = 0x00;

        // Week and Year (bytes 16..17)
        edid[16] = 10;
        edid[17] = 30; // 1990 + 30 = 2020

        // Version 1.4 (bytes 18..19)
        edid[18] = 1;
        edid[19] = 4;

        // Basic display parameters (bytes 20..24)
        edid[20] = 0xA5; // Digital display
        edid[21] = (byte)widthCm;
        edid[22] = (byte)heightCm;
        edid[23] = 120;  // Gamma 2.2
        edid[24] = 0x02; // Feature support

        // Block 0 (bytes 54..71): Preferred Detailed Timing Descriptor
        if (pixelClock10Khz > 0)
        {
            edid[54] = (byte)(pixelClock10Khz & 0xFF);
            edid[55] = (byte)((pixelClock10Khz >> 8) & 0xFF);
            edid[56] = (byte)(hActive & 0xFF);
            edid[57] = (byte)(hBlank & 0xFF);
            edid[58] = (byte)(((hActive >> 4) & 0xF0) | ((hBlank >> 8) & 0x0F));
            edid[59] = (byte)(vActive & 0xFF);
            edid[60] = (byte)(vBlank & 0xFF);
            edid[61] = (byte)(((vActive >> 4) & 0xF0) | ((vBlank >> 8) & 0x0F));
            edid[71] = (byte)(isInterlaced ? 0x80 : 0x00);
        }

        // Block 1 (bytes 72..89): Monitor Name (0xFC)
        if (!string.IsNullOrEmpty(modelName))
        {
            edid[72] = 0x00;
            edid[73] = 0x00;
            edid[74] = 0x00;
            edid[75] = 0xFC;
            edid[76] = 0x00;

            byte[] nameBytes = Encoding.ASCII.GetBytes(modelName);
            int copyLen = Math.Min(nameBytes.Length, 13);
            Array.Copy(nameBytes, 0, edid, 77, copyLen);
            if (copyLen < 13)
            {
                edid[77 + copyLen] = 0x0A; // Terminating newline
                for (int p = copyLen + 1; p < 13; p++)
                {
                    edid[77 + p] = 0x20; // Padding spaces
                }
            }
        }

        // Block 2 (bytes 90..107): Monitor Serial Number (0xFF)
        if (!string.IsNullOrEmpty(serialNumber))
        {
            edid[90] = 0x00;
            edid[91] = 0x00;
            edid[92] = 0x00;
            edid[93] = 0xFF;
            edid[94] = 0x00;

            byte[] serialBytes = Encoding.ASCII.GetBytes(serialNumber);
            int copyLen = Math.Min(serialBytes.Length, 13);
            Array.Copy(serialBytes, 0, edid, 95, copyLen);
            if (copyLen < 13)
            {
                edid[95 + copyLen] = 0x0A; // Terminating newline
                for (int p = copyLen + 1; p < 13; p++)
                {
                    edid[95 + p] = 0x20; // Padding spaces
                }
            }
        }

        // Checksum calculation (byte 127): sum(0..127) % 256 == 0
        int sum = 0;
        for (int i = 0; i < 127; i++)
        {
            sum += edid[i];
        }
        edid[127] = (byte)((256 - (sum % 256)) % 256);

        return edid;
    }

    [Fact]
    public void Parse_NullOrEmptyOrTooShort_ReturnsNull()
    {
        Assert.Null(EdidParser.Parse(null));
        Assert.Null(EdidParser.Parse(Array.Empty<byte>()));
        Assert.Null(EdidParser.Parse(new byte[64]));
        Assert.Null(EdidParser.Parse(new byte[127]));
    }

    [Fact]
    public void Parse_InvalidHeader_ReturnsNull()
    {
        var edid = CreateEdid();
        edid[0] = 0x01; // Header must start with 0x00
        Assert.Null(EdidParser.Parse(edid));

        var edid2 = CreateEdid();
        edid2[1] = 0xFE; // Bytes 1..6 must be 0xFF
        Assert.Null(EdidParser.Parse(edid2));

        var edid3 = CreateEdid();
        edid3[7] = 0x01; // Byte 7 must be 0x00
        Assert.Null(EdidParser.Parse(edid3));
    }

    [Fact]
    public void Parse_InvalidChecksum_ReturnsNull()
    {
        var edid = CreateEdid();
        edid[127] ^= 0x01; // Invalidate checksum byte
        Assert.Null(EdidParser.Parse(edid));
    }

    [Fact]
    public void Parse_Valid128ByteDell27Inch1440p144Hz_ParsesAllFieldsCorrectly()
    {
        // Dell 27" 2560x1440 @ 144Hz
        // 60 cm x 34 cm -> diagonal ~27.2", aspect 16:9
        var edid = CreateEdid(
            vendor: "DEL",
            widthCm: 60,
            heightCm: 34,
            hActive: 2560,
            vActive: 1440,
            hBlank: 160,
            vBlank: 41,
            pixelClock10Khz: 58009,
            isInterlaced: false,
            modelName: "DELL U2720Q",
            serialNumber: "CN-012345");

        var result = EdidParser.Parse(edid);

        Assert.NotNull(result);
        Assert.Equal("DEL", result.ManufacturerId);
        Assert.Equal("DELL U2720Q", result.ModelName);
        Assert.Equal("CN-012345", result.SerialNumber);
        Assert.Equal(60, result.PhysicalWidthCm);
        Assert.Equal(34, result.PhysicalHeightCm);
        Assert.Equal(27.2, result.DiagonalInches);
        Assert.Equal("16:9", result.AspectRatio);

        Assert.NotNull(result.PreferredMode);
        Assert.Equal(2560, result.PreferredMode.Width);
        Assert.Equal(1440, result.PreferredMode.Height);
        Assert.Equal(144.0, result.PreferredMode.RefreshHz);
        Assert.False(result.PreferredMode.IsInterlaced);
    }

    [Fact]
    public void Parse_ValidUltrawide34Inch21By9_ParsesAspectRatioAndMode()
    {
        // 34" 3440x1440, 80cm x 34cm -> diagonal = sqrt(80^2 + 34^2)/2.54 = 86.925/2.54 = 34.2"
        // 80/34 = 2.3529 -> snapped to 21:9
        // 3440x1440 @ 100Hz: hBlank = 160 (hTotal = 3600), vBlank = 45 (vTotal = 1485)
        // pixelClock = 3600 * 1485 * 100 = 534,600,000 Hz = 53460 in 10kHz
        var edid = CreateEdid(
            vendor: "SAM",
            widthCm: 80,
            heightCm: 34,
            hActive: 3440,
            vActive: 1440,
            hBlank: 160,
            vBlank: 45,
            pixelClock10Khz: 53460,
            isInterlaced: false,
            modelName: "Odyssey G9",
            serialNumber: "SAM-998877");

        var result = EdidParser.Parse(edid);

        Assert.NotNull(result);
        Assert.Equal("SAM", result.ManufacturerId);
        Assert.Equal("Odyssey G9", result.ModelName);
        Assert.Equal("SAM-998877", result.SerialNumber);
        Assert.Equal(80, result.PhysicalWidthCm);
        Assert.Equal(34, result.PhysicalHeightCm);
        Assert.Equal(34.2, result.DiagonalInches);
        Assert.Equal("21:9", result.AspectRatio);

        Assert.NotNull(result.PreferredMode);
        Assert.Equal(3440, result.PreferredMode.Width);
        Assert.Equal(1440, result.PreferredMode.Height);
        Assert.Equal(100.0, result.PreferredMode.RefreshHz);
        Assert.False(result.PreferredMode.IsInterlaced);
    }

    [Fact]
    public void Parse_Valid16By101920x1200_ParsesCorrectly()
    {
        // 1920x1200, 52cm x 32cm -> diagonal = sqrt(52^2 + 32^2)/2.54 = sqrt(3728)/2.54 = 61.057/2.54 = 24.0"
        // 52/32 = 1.625 -> snapped to 16:10
        // hBlank = 160 (hTotal = 2080), vBlank = 35 (vTotal = 1235)
        // pixelClock = 2080 * 1235 * 60 = 154,128,000 Hz = 15413 in 10kHz
        var edid = CreateEdid(
            vendor: "ACR",
            widthCm: 52,
            heightCm: 32,
            hActive: 1920,
            vActive: 1200,
            hBlank: 160,
            vBlank: 35,
            pixelClock10Khz: 15413,
            isInterlaced: false,
            modelName: "Acer Pro",
            serialNumber: null);

        var result = EdidParser.Parse(edid);

        Assert.NotNull(result);
        Assert.Equal("ACR", result.ManufacturerId);
        Assert.Equal("Acer Pro", result.ModelName);
        Assert.Null(result.SerialNumber);
        Assert.Equal(52, result.PhysicalWidthCm);
        Assert.Equal(32, result.PhysicalHeightCm);
        Assert.Equal(24.0, result.DiagonalInches);
        Assert.Equal("16:10", result.AspectRatio);

        Assert.NotNull(result.PreferredMode);
        Assert.Equal(1920, result.PreferredMode.Width);
        Assert.Equal(1200, result.PreferredMode.Height);
        Assert.Equal(60.0, result.PreferredMode.RefreshHz);
        Assert.False(result.PreferredMode.IsInterlaced);
    }

    [Fact]
    public void Parse_ValidSuperUltrawide32By9_SnapsTo32By9()
    {
        // 49" 5120x1440, 119cm x 34cm -> diagonal = sqrt(119^2 + 34^2)/2.54 = 123.76/2.54 = 48.7"
        // 119/34 = 3.5 -> snapped to 32:9 (3.5556)
        var edid = CreateEdid(
            vendor: "SAM",
            widthCm: 119,
            heightCm: 34,
            hActive: 5120,
            vActive: 1440,
            hBlank: 160,
            vBlank: 45,
            pixelClock10Khz: 79398,
            isInterlaced: false,
            modelName: "Samsung 49",
            serialNumber: "SN123");

        var result = EdidParser.Parse(edid);

        Assert.NotNull(result);
        Assert.Equal(48.7, result.DiagonalInches);
        Assert.Equal("32:9", result.AspectRatio);
    }

    [Fact]
    public void Parse_Valid4By3And5By4_SnapsCorrectly()
    {
        // 40cm x 30cm -> 1.3333 -> 4:3
        var edid43 = CreateEdid(
            vendor: "DEL",
            widthCm: 40,
            heightCm: 30,
            hActive: 1600,
            vActive: 1200,
            hBlank: 160,
            vBlank: 40,
            pixelClock10Khz: 13104);

        var result43 = EdidParser.Parse(edid43);
        Assert.NotNull(result43);
        Assert.Equal("4:3", result43.AspectRatio);
        Assert.Equal(19.7, result43.DiagonalInches);

        // 38cm x 30cm -> 1.2667 -> 5:4 (1.25)
        var edid54 = CreateEdid(
            vendor: "DEL",
            widthCm: 38,
            heightCm: 30,
            hActive: 1280,
            vActive: 1024,
            hBlank: 160,
            vBlank: 40,
            pixelClock10Khz: 9216);

        var result54 = EdidParser.Parse(edid54);
        Assert.NotNull(result54);
        Assert.Equal("5:4", result54.AspectRatio);
        Assert.Equal(19.1, result54.DiagonalInches);
    }

    [Fact]
    public void Parse_InterlacedMode_SetsIsInterlacedTrue()
    {
        var edid = CreateEdid(
            hActive: 1920,
            vActive: 1080,
            hBlank: 280,
            vBlank: 45,
            pixelClock10Khz: 7425,
            isInterlaced: true);

        var result = EdidParser.Parse(edid);

        Assert.NotNull(result);
        Assert.NotNull(result.PreferredMode);
        Assert.True(result.PreferredMode.IsInterlaced);
    }

    [Fact]
    public void Parse_ZeroDimensions_SetsDiagonalZeroAndEmptyAspectRatio()
    {
        var edid = CreateEdid(widthCm: 0, heightCm: 0);
        var result = EdidParser.Parse(edid);

        Assert.NotNull(result);
        Assert.Equal(0, result.PhysicalWidthCm);
        Assert.Equal(0, result.PhysicalHeightCm);
        Assert.Equal(0.0, result.DiagonalInches);
        Assert.Equal(string.Empty, result.AspectRatio);
    }

    [Fact]
    public void Parse_256ByteEdid_ParsesBase128BlockSuccessfully()
    {
        var baseEdid = CreateEdid(vendor: "DEL", modelName: "Dell Extended");
        var extendedEdid = new byte[256];
        Array.Copy(baseEdid, 0, extendedEdid, 0, 128);
        extendedEdid[126] = 1; // 1 extension block
        // Recompute base block checksum with byte 126 = 1
        int sum = 0;
        for (int i = 0; i < 127; i++) sum += extendedEdid[i];
        extendedEdid[127] = (byte)((256 - (sum % 256)) % 256);

        var result = EdidParser.Parse(extendedEdid);

        Assert.NotNull(result);
        Assert.Equal("DEL", result.ManufacturerId);
        Assert.Equal("Dell Extended", result.ModelName);
    }
}
