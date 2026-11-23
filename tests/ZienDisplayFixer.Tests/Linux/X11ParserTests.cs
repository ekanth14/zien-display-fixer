namespace ZienDisplayFixer.Tests.Linux;

using System.Linq;
using Xunit;
using ZienDisplayFixer.Core.Linux;
using ZienDisplayFixer.Core.Models;

public class X11ParserTests
{
    private const string SampleXRandrOutput = """
        Screen 0: minimum 320 x 200, current 4480 x 1440, maximum 16384 x 16384
        eDP-1 connected primary 1920x1080+0+0 (normal left inverted right x axis y axis) 344mm x 193mm
           1920x1080     60.00*+  59.97    59.96    59.93    48.00  
           1680x1050     59.95    59.88  
           1400x1050     59.98  
           1600x900      59.99    59.94    59.95    59.82  
           1280x1024     60.02  
           1280x720      60.00    59.99    59.86    59.74  
           1024x768      60.04    60.00  
           800x600       60.32    56.25  
           640x480       60.00    59.94  
        HDMI-1 connected 2560x1440+1920+0 (normal left inverted right x axis y axis) 597mm x 336mm
           2560x1440     59.95*+  74.97  
           1920x1080    119.88    60.00    59.94    50.00  
           1280x720      60.00    59.94    50.00  
           1024x768      75.03    60.00  
        DP-1 disconnected (normal left inverted right x axis y axis)
        DP-2 disconnected
        """;

    [Fact]
    public void ParseXRandrQuery_MultiMonitorOutput_ExtractsConnectedDisplaysAndIgnoresDisconnected()
    {
        var displays = X11Parser.ParseXRandrQuery(SampleXRandrOutput);

        Assert.NotNull(displays);
        Assert.Equal(2, displays.Count);

        var primary = displays.FirstOrDefault(d => d.Id == "eDP-1");
        Assert.NotNull(primary);
        Assert.Equal("eDP-1", primary.Name);
        Assert.True(primary.IsPrimary);
        Assert.Equal(0, primary.PositionX);
        Assert.Equal(0, primary.PositionY);
        Assert.Equal(344, primary.PhysicalWidthMm);
        Assert.Equal(193, primary.PhysicalHeightMm);
        Assert.Equal(ConnectionType.DisplayPort, primary.Connection);
        Assert.NotNull(primary.CurrentMode);
        Assert.Equal(1920, primary.CurrentMode.Width);
        Assert.Equal(1080, primary.CurrentMode.Height);
        Assert.Equal(60.00, primary.CurrentMode.RefreshHz);
        Assert.False(primary.CurrentMode.IsInterlaced);

        var secondary = displays.FirstOrDefault(d => d.Id == "HDMI-1");
        Assert.NotNull(secondary);
        Assert.Equal("HDMI-1", secondary.Name);
        Assert.False(secondary.IsPrimary);
        Assert.Equal(1920, secondary.PositionX);
        Assert.Equal(0, secondary.PositionY);
        Assert.Equal(597, secondary.PhysicalWidthMm);
        Assert.Equal(336, secondary.PhysicalHeightMm);
        Assert.Equal(ConnectionType.Hdmi, secondary.Connection);
        Assert.NotNull(secondary.CurrentMode);
        Assert.Equal(2560, secondary.CurrentMode.Width);
        Assert.Equal(1440, secondary.CurrentMode.Height);
        Assert.Equal(59.95, secondary.CurrentMode.RefreshHz);
        Assert.False(secondary.CurrentMode.IsInterlaced);
    }

    [Fact]
    public void ParseXRandrModes_SpecificDisplay_ExtractsAllAvailableModes()
    {
        var modes = X11Parser.ParseXRandrModes(SampleXRandrOutput, "HDMI-1");

        Assert.NotNull(modes);
        Assert.NotEmpty(modes);

        // HDMI-1 has:
        // 2560x1440 @ 59.95, 74.97
        // 1920x1080 @ 119.88, 60.00, 59.94, 50.00
        // 1280x720  @ 60.00, 59.94, 50.00
        // 1024x768  @ 75.03, 60.00
        // Total = 2 + 4 + 3 + 2 = 11 modes
        Assert.Equal(11, modes.Count);

        Assert.Contains(modes, m => m.Width == 2560 && m.Height == 1440 && m.RefreshHz == 59.95);
        Assert.Contains(modes, m => m.Width == 2560 && m.Height == 1440 && m.RefreshHz == 74.97);
        Assert.Contains(modes, m => m.Width == 1920 && m.Height == 1080 && m.RefreshHz == 119.88);
        Assert.Contains(modes, m => m.Width == 1920 && m.Height == 1080 && m.RefreshHz == 60.00);
    }

    [Fact]
    public void ParseXRandrModes_NonExistentDisplay_ReturnsEmptyList()
    {
        var modes = X11Parser.ParseXRandrModes(SampleXRandrOutput, "VGA-1");

        Assert.NotNull(modes);
        Assert.Empty(modes);
    }

    [Fact]
    public void ParseXRandrQuery_EmptyOrNullInput_ReturnsEmptyList()
    {
        Assert.Empty(X11Parser.ParseXRandrQuery(string.Empty));
        Assert.Empty(X11Parser.ParseXRandrQuery(null!));
        Assert.Empty(X11Parser.ParseXRandrModes(string.Empty, "eDP-1"));
        Assert.Empty(X11Parser.ParseXRandrModes(null!, "eDP-1"));
    }

    [Fact]
    public void ParseXRandrQuery_ConnectedDisplayWithoutMode_ReturnsDisplayWithNullCurrentMode()
    {
        const string output = """
            Screen 0: minimum 320 x 200, current 1920 x 1080, maximum 16384 x 16384
            HDMI-2 connected (normal left inverted right x axis y axis)
               1920x1080     60.00    50.00  
            """;

        var displays = X11Parser.ParseXRandrQuery(output);
        Assert.Single(displays);
        var display = displays[0];
        Assert.Equal("HDMI-2", display.Id);
        Assert.Null(display.CurrentMode);
        Assert.False(display.IsPrimary);
    }

    [Fact]
    public void ParseXRandrModes_InterlacedModes_SetsIsInterlacedFlag()
    {
        const string output = """
            Screen 0: minimum 320 x 200, current 1920 x 1080, maximum 16384 x 16384
            HDMI-1 connected primary 1920x1080+0+0 (normal left inverted right x axis y axis) 597mm x 336mm
               1920x1080i    60.00*+  50.00i 
               1280x720      60.00  
            """;

        var modes = X11Parser.ParseXRandrModes(output, "HDMI-1");
        Assert.Equal(3, modes.Count);

        var mode1 = modes.First(m => m.Width == 1920 && m.Height == 1080 && m.RefreshHz == 60.00);
        Assert.True(mode1.IsInterlaced);

        var mode2 = modes.First(m => m.Width == 1920 && m.Height == 1080 && m.RefreshHz == 50.00);
        Assert.True(mode2.IsInterlaced);

        var mode3 = modes.First(m => m.Width == 1280 && m.Height == 720);
        Assert.False(mode3.IsInterlaced);
    }

    [Fact]
    public void ParseXRandrQuery_NegativeCoordinates_ParsesPositionXAndPositionYCorrectly()
    {
        const string output = """
            Screen 0: minimum 320 x 200, current 4480 x 1440, maximum 16384 x 16384
            HDMI-1 connected 2560x1440-2560+0 (normal left inverted right x axis y axis) 597mm x 336mm
               2560x1440     59.95*+
            DP-1 connected 1920x1080+0-1080 (normal left inverted right x axis y axis) 500mm x 300mm
               1920x1080     60.00*+
            """;

        var displays = X11Parser.ParseXRandrQuery(output);
        Assert.Equal(2, displays.Count);

        var hdmi = displays.First(d => d.Id == "HDMI-1");
        Assert.Equal(-2560, hdmi.PositionX);
        Assert.Equal(0, hdmi.PositionY);
        Assert.NotNull(hdmi.CurrentMode);
        Assert.Equal(2560, hdmi.CurrentMode.Width);
        Assert.Equal(1440, hdmi.CurrentMode.Height);

        var dp = displays.First(d => d.Id == "DP-1");
        Assert.Equal(0, dp.PositionX);
        Assert.Equal(-1080, dp.PositionY);
        Assert.NotNull(dp.CurrentMode);
        Assert.Equal(1920, dp.CurrentMode.Width);
        Assert.Equal(1080, dp.CurrentMode.Height);
    }

    [Theory]
    [InlineData("HDMI-1", ConnectionType.Hdmi)]
    [InlineData("HDMI-A-1", ConnectionType.Hdmi)]
    [InlineData("DP-1", ConnectionType.DisplayPort)]
    [InlineData("eDP-1", ConnectionType.DisplayPort)]
    [InlineData("VGA-1", ConnectionType.Vga)]
    [InlineData("DVI-D-1", ConnectionType.Unknown)]
    [InlineData("Virtual-1", ConnectionType.Unknown)]
    public void ParseXRandrQuery_ConnectionTypes_MapsCorrectly(string portName, ConnectionType expectedType)
    {
        string output = $"""
            Screen 0: minimum 320 x 200, current 1920 x 1080, maximum 16384 x 16384
            {portName} connected 1920x1080+0+0 (normal left inverted right x axis y axis) 500mm x 300mm
               1920x1080     60.00*+
            """;

        var displays = X11Parser.ParseXRandrQuery(output);
        Assert.Single(displays);
        Assert.Equal(expectedType, displays[0].Connection);
    }
}
