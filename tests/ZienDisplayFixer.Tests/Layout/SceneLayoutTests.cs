namespace ZienDisplayFixer.Tests.Layout;

using Xunit;
using ZienDisplayFixer.Core.Layout;
using ZienDisplayFixer.Core.Models;

public class SceneLayoutTests
{
    private static DisplayInfo CreateDisplay(
        string id,
        string model,
        int width,
        int height,
        double refreshHz,
        double physWidthMm = 597.0,
        double physHeightMm = 336.0,
        ConnectionType conn = ConnectionType.Hdmi)
    {
        return new DisplayInfo(
            id: id,
            name: model,
            model: model,
            isPrimary: id == "1",
            positionX: 0,
            positionY: 0,
            currentMode: new DisplayMode(width, height, refreshHz),
            physicalWidthMm: physWidthMm,
            physicalHeightMm: physHeightMm,
            connection: conn);
    }

    [Fact]
    public void Calculate_LaptopHost_CenteredAt500x265_WithCorrectDimensions()
    {
        var result = SceneLayout.Calculate(DeviceType.Laptop, 14.0, []);

        Assert.Equal(1000.0, result.ViewportWidth);
        Assert.Equal(560.0, result.ViewportHeight);
        Assert.True(result.Device.IsLaptop);
        Assert.Equal("Laptop", result.Device.Label);

        // Center check
        double devCenterX = result.Device.X + result.Device.Width / 2.0;
        double devCenterY = result.Device.Y + result.Device.Height / 2.0;
        Assert.Equal(500.0, devCenterX, 0.001);
        Assert.Equal(265.0, devCenterY, 0.001);

        // Scaling factor s should be clamped to 11.0 when no monitors
        // dw = 14.0 * 0.88 = 12.32, dh = 12.32 * 0.7 = 8.624
        // With s = 11.0: Width = 135.52, Height = 94.864
        Assert.Equal(14.0 * 0.88 * 11.0, result.Device.Width, 0.01);
        Assert.Equal(14.0 * 0.88 * 0.7 * 11.0, result.Device.Height, 0.01);
        Assert.True(result.Device.PortY > result.Device.Y && result.Device.PortY < result.Device.Y + result.Device.Height);
    }

    [Fact]
    public void Calculate_PcHost_CenteredAt500x265_WithCorrectDimensions()
    {
        var result = SceneLayout.Calculate(DeviceType.Pc, 0, []);

        Assert.False(result.Device.IsLaptop);
        Assert.Equal("PC", result.Device.Label);

        double devCenterX = result.Device.X + result.Device.Width / 2.0;
        double devCenterY = result.Device.Y + result.Device.Height / 2.0;
        Assert.Equal(500.0, devCenterX, 0.001);
        Assert.Equal(265.0, devCenterY, 0.001);

        // For PC: dw = 8, dh = 17, with s = 11.0 -> Width = 88, Height = 187
        Assert.Equal(8.0 * 11.0, result.Device.Width, 0.01);
        Assert.Equal(17.0 * 11.0, result.Device.Height, 0.01);
    }

    [Fact]
    public void Calculate_AlternatingLeftRight_DistributesMonitorsCorrectly()
    {
        var d1 = CreateDisplay("1", "Dell 27", 2560, 1440, 144);
        var d2 = CreateDisplay("2", "LG 27", 2560, 1440, 60);

        // With Laptop: Laptop is screen #1, monitors become #2 (left) and #3 (right)
        var result = SceneLayout.Calculate(DeviceType.Laptop, 15.6, [d1, d2]);

        Assert.Equal(2, result.Monitors.Count);
        Assert.Equal(2, result.Cables.Count);

        var m0 = result.Monitors[0];
        var m1 = result.Monitors[1];

        // Monitor 0 should be on the Left side (X + Width < Device.X)
        Assert.True(m0.X + m0.Width <= result.Device.X, "Monitor 0 must be to the left of the host device");
        Assert.Equal(2, m0.DisplayNumber);

        // Monitor 1 should be on the Right side (X >= Device.X + Device.Width)
        Assert.True(m1.X >= result.Device.X + result.Device.Width, "Monitor 1 must be to the right of the host device");
        Assert.Equal(3, m1.DisplayNumber);
    }

    [Fact]
    public void Calculate_PcHost_DisplayNumbersStartAt1()
    {
        var d1 = CreateDisplay("1", "Dell 27", 2560, 1440, 144);
        var d2 = CreateDisplay("2", "LG 27", 2560, 1440, 60);

        var result = SceneLayout.Calculate(DeviceType.Pc, 0, [d1, d2]);

        Assert.Equal(1, result.Monitors[0].DisplayNumber);
        Assert.Equal(2, result.Monitors[1].DisplayNumber);
    }

    [Fact]
    public void Calculate_MultiColumnStacking_CreatesOutwardColumnWhenExceeding3()
    {
        // 7 external monitors:
        // Left side gets 4 monitors: indices 0, 2, 4, 6
        // Right side gets 3 monitors: indices 1, 3, 5
        var displays = new List<DisplayInfo>();
        for (int i = 0; i < 7; i++)
        {
            displays.Add(CreateDisplay($"d{i}", $"Monitor {i}", 1920, 1080, 60));
        }

        var result = SceneLayout.Calculate(DeviceType.Laptop, 14.0, displays);

        Assert.Equal(7, result.Monitors.Count);

        // Monitors at indices 0, 2, 4 are column 0 on the left (stacked vertically)
        var m0 = result.Monitors[0];
        var m2 = result.Monitors[2];
        var m4 = result.Monitors[4];
        var m6 = result.Monitors[6]; // 4th monitor on left -> column 1 (outward to the left)

        // Same column should have same X or stand X alignment
        Assert.Equal(m0.X, m2.X, 0.01);
        Assert.Equal(m0.X, m4.X, 0.01);

        // Vertical order: m0, m2, m4 stacked top to bottom
        Assert.True(m0.Y < m2.Y);
        Assert.True(m2.Y < m4.Y);

        // m6 is in column 1 (outward to the left), so its X must be further left than m0.X
        Assert.True(m6.X + m6.Width <= m0.X, "Column 1 must be located further left than Column 0");
    }

    [Fact]
    public void Calculate_BoundsScaling_AllElementsFitWithinViewport()
    {
        // Test with 8 large displays
        var displays = new List<DisplayInfo>();
        for (int i = 0; i < 8; i++)
        {
            displays.Add(CreateDisplay($"d{i}", $"Ultra {i}", 3840, 2160, 60, physWidthMm: 708.0, physHeightMm: 398.0));
        }

        var result = SceneLayout.Calculate(DeviceType.Laptop, 16.0, displays);

        // Check device fits in 1000 x 560
        Assert.True(result.Device.X >= 0, "Device.X should be >= 0");
        Assert.True(result.Device.X + result.Device.Width <= 1000.0, "Device right should be <= 1000");
        Assert.True(result.Device.Y >= 0, "Device.Y should be >= 0");
        Assert.True(result.Device.Y + result.Device.Height <= 560.0, "Device bottom should be <= 560");

        // Check each monitor fits within viewport bounds
        foreach (var m in result.Monitors)
        {
            Assert.True(m.X >= 0, $"Monitor {m.DisplayNumber} X ({m.X}) should be >= 0");
            Assert.True(m.X + m.Width <= 1000.0, $"Monitor {m.DisplayNumber} right ({m.X + m.Width}) should be <= 1000");
            Assert.True(m.Y >= 0, $"Monitor {m.DisplayNumber} Y ({m.Y}) should be >= 0");
            Assert.True(m.Y + m.Height <= 560.0, $"Monitor {m.DisplayNumber} bottom ({m.Y + m.Height}) should be <= 560");

            // Check inner screen fits inside outer bounds
            Assert.True(m.ScreenX >= m.X);
            Assert.True(m.ScreenX + m.ScreenWidth <= m.X + m.Width);
            Assert.True(m.ScreenY >= m.Y);
            Assert.True(m.ScreenY + m.ScreenHeight <= m.Y + m.Height);

            // Stand & base alignment
            Assert.True(m.StandY >= m.ScreenY + m.ScreenHeight);
            Assert.True(m.BaseY >= m.StandY);
            Assert.True(m.BaseY + m.BaseHeight <= m.Y + m.Height);
        }
    }

    [Fact]
    public void Calculate_CablesAlignWithDevicePortsAndMonitorStands()
    {
        var dLeft = CreateDisplay("1", "Left Screen", 2560, 1440, 144, conn: ConnectionType.DisplayPort);
        var dRight = CreateDisplay("2", "Right Screen", 1920, 1080, 60, conn: ConnectionType.WiFi);

        var result = SceneLayout.Calculate(DeviceType.Laptop, 14.0, [dLeft, dRight]);

        Assert.Equal(2, result.Cables.Count);

        var cLeft = result.Cables[0];
        var mLeft = result.Monitors[0];

        // Cable start on left side of device
        Assert.Equal(result.Device.X, cLeft.X0, 0.01);
        Assert.Equal(result.Device.PortY, cLeft.Y0, 1.0);

        // Cable end at center of monitor stand base
        double baseCenterX = mLeft.BaseX + mLeft.BaseWidth / 2.0;
        Assert.Equal(baseCenterX, cLeft.Xe, 0.01);
        Assert.True(cLeft.Ye >= mLeft.BaseY && cLeft.Ye <= mLeft.BaseY + mLeft.BaseHeight);

        // Wired connection
        Assert.False(cLeft.IsWireless);
        Assert.Equal("DP", cLeft.ConnectorText);
        Assert.True(cLeft.PillWidth > 0);
        Assert.True(cLeft.PillHeight > 0);

        // Right connection is Wireless WiFi
        var cRight = result.Cables[1];
        Assert.True(cRight.IsWireless);
        Assert.Equal("Wi-Fi", cRight.ConnectorText);
        Assert.Equal(result.Device.X + result.Device.Width, cRight.X0, 0.01);
    }

    [Fact]
    public void Calculate_AspectRatioSnapping_RecognizesStandardRatios()
    {
        // 16:9
        var d169 = CreateDisplay("1", "16:9", 1920, 1080, 60, 597.0, 336.0);
        // 16:10
        var d1610 = CreateDisplay("2", "16:10", 1920, 1200, 60, 517.0, 323.0);
        // 21:9
        var d219 = CreateDisplay("3", "21:9", 3440, 1440, 100, 797.0, 334.0);
        // 32:9
        var d329 = CreateDisplay("4", "32:9", 5120, 1440, 120, 1192.0, 335.0);

        var result = SceneLayout.Calculate(DeviceType.Pc, 0, [d169, d1610, d219, d329]);

        Assert.Equal("16:9", result.Monitors[0].AspectRatio);
        Assert.Equal("16:10", result.Monitors[1].AspectRatio);
        Assert.Equal("21:9", result.Monitors[2].AspectRatio);
        Assert.Equal("32:9", result.Monitors[3].AspectRatio);
    }
}
