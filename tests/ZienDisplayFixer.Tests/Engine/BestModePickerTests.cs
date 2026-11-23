using Xunit;
using ZienDisplayFixer.Core.Engine;
using ZienDisplayFixer.Core.Models;

namespace ZienDisplayFixer.Tests.Engine;

public class BestModePickerTests
{
    private DisplayInfo CreateDisplay(ConnectionType connection = ConnectionType.DisplayPort) =>
        new DisplayInfo
        {
            Id = "disp-1",
            Name = "Test Monitor",
            Model = "UltraSharp",
            IsPrimary = true,
            PositionX = 0,
            PositionY = 0,
            PhysicalWidthMm = 600,
            PhysicalHeightMm = 340,
            Connection = connection
        };

    [Fact]
    public void PickBestMode_PicksHighestResolutionFirst()
    {
        // Arrange
        var display = CreateDisplay(ConnectionType.Hdmi);
        var modes = new List<DisplayMode>
        {
            new DisplayMode(1920, 1080, 60),
            new DisplayMode(3840, 2160, 60),
            new DisplayMode(2560, 1440, 60),
        };

        // Act
        var best = BestModePicker.PickBestMode(modes, display);

        // Assert
        Assert.NotNull(best);
        Assert.Equal(3840, best.Width);
        Assert.Equal(2160, best.Height);
        Assert.Equal(60, best.RefreshHz);
    }

    [Fact]
    public void PickBestMode_PicksHighestRefreshRate_WhenResolutionIsTied()
    {
        // Arrange
        var display = CreateDisplay(ConnectionType.DisplayPort);
        var modes = new List<DisplayMode>
        {
            new DisplayMode(2560, 1440, 60),
            new DisplayMode(2560, 1440, 144),
            new DisplayMode(2560, 1440, 120),
        };

        // Act
        var best = BestModePicker.PickBestMode(modes, display);

        // Assert
        Assert.NotNull(best);
        Assert.Equal(2560, best.Width);
        Assert.Equal(1440, best.Height);
        Assert.Equal(144, best.RefreshHz);
    }

    [Fact]
    public void PickBestMode_IgnoresInterlacedModes()
    {
        // Arrange
        var display = CreateDisplay(ConnectionType.Hdmi);
        var modes = new List<DisplayMode>
        {
            new DisplayMode(3840, 2160, 60, isInterlaced: true), // Interlaced 4K
            new DisplayMode(1920, 1080, 60, isInterlaced: false), // Progressive 1080p
        };

        // Act
        var best = BestModePicker.PickBestMode(modes, display);

        // Assert
        Assert.NotNull(best);
        Assert.Equal(1920, best.Width);
        Assert.Equal(1080, best.Height);
        Assert.False(best.IsInterlaced);
    }

    [Theory]
    [InlineData(ConnectionType.Vga)]
    [InlineData(ConnectionType.WiFi)]
    public void PickBestMode_CapsAt1080p60_ForVgaAndWiFi(ConnectionType connection)
    {
        // Arrange
        var display = CreateDisplay(connection);
        var modes = new List<DisplayMode>
        {
            new DisplayMode(3840, 2160, 60), // Exceeds resolution cap
            new DisplayMode(2560, 1440, 144), // Exceeds resolution and refresh cap
            new DisplayMode(1920, 1080, 144), // Exceeds refresh cap (144Hz > 60Hz)
            new DisplayMode(1920, 1080, 60),  // Exact cap match
            new DisplayMode(1280, 720, 60),   // Lower resolution
        };

        // Act
        var best = BestModePicker.PickBestMode(modes, display);

        // Assert
        Assert.NotNull(best);
        Assert.Equal(1920, best.Width);
        Assert.Equal(1080, best.Height);
        Assert.Equal(60, best.RefreshHz);
    }

    [Fact]
    public void PickBestMode_ReturnsNull_WhenModesListIsEmpty()
    {
        // Arrange
        var display = CreateDisplay();
        var modes = Enumerable.Empty<DisplayMode>();

        // Act
        var best = BestModePicker.PickBestMode(modes, display);

        // Assert
        Assert.Null(best);
    }

    [Fact]
    public void PickBestMode_ReturnsNull_WhenAllModesAreInterlaced()
    {
        // Arrange
        var display = CreateDisplay();
        var modes = new List<DisplayMode>
        {
            new DisplayMode(1920, 1080, 60, isInterlaced: true),
            new DisplayMode(1280, 720, 60, isInterlaced: true)
        };

        // Act
        var best = BestModePicker.PickBestMode(modes, display);

        // Assert
        Assert.Null(best);
    }
}
