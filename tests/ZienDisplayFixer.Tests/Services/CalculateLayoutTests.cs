namespace ZienDisplayFixer.Tests.Services;

using Xunit;
using ZienDisplayFixer.App.Services.Windows;
using ZienDisplayFixer.Core.Models;

public class CalculateLayoutTests
{
    [Fact]
    public void NonOverlappingDisplays_PreserveOriginalPositions()
    {
        var d1 = new DisplayInfo(@"\\.\DISPLAY1", "Display 1", "Model 1", true, 0, 0, new DisplayMode(1920, 1080, 60, false), 0, 0, ConnectionType.Hdmi);
        var d2 = new DisplayInfo(@"\\.\DISPLAY2", "Display 2", "Model 2", false, 1920, 0, new DisplayMode(1920, 1080, 60, false), 0, 0, ConnectionType.DisplayPort);

        var newMode = new DisplayMode(1920, 1080, 144, false);
        var layouts = WindowsDisplayService.CalculateLayout(new[] { d1, d2 }, d2.Id, newMode);

        Assert.Equal(2, layouts.Count);
        var l1 = layouts.Single(l => l.DisplayId == d1.Id);
        var l2 = layouts.Single(l => l.DisplayId == d2.Id);

        Assert.Equal(0, l1.PositionX);
        Assert.Equal(0, l1.PositionY);
        Assert.Equal(1920, l2.PositionX);
        Assert.Equal(0, l2.PositionY);
        Assert.Equal(144u, l2.TargetRefreshHz);
    }

    [Fact]
    public void OverlappingDisplays_RetilesLeftToRightWithPrimaryAtOrigin()
    {
        // When display 1 resolution increases from 1920 to 2560, it overlaps display 2 at X=1920
        var d1 = new DisplayInfo(@"\\.\DISPLAY1", "Display 1", "Model 1", true, 0, 0, new DisplayMode(1920, 1080, 60, false), 0, 0, ConnectionType.Hdmi);
        var d2 = new DisplayInfo(@"\\.\DISPLAY2", "Display 2", "Model 2", false, 1920, 0, new DisplayMode(1920, 1080, 60, false), 0, 0, ConnectionType.DisplayPort);

        var upgradeMode = new DisplayMode(2560, 1440, 144, false);
        var layouts = WindowsDisplayService.CalculateLayout(new[] { d1, d2 }, d1.Id, upgradeMode);

        Assert.Equal(2, layouts.Count);
        var l1 = layouts.Single(l => l.DisplayId == d1.Id);
        var l2 = layouts.Single(l => l.DisplayId == d2.Id);

        Assert.Equal(0, l1.PositionX);
        Assert.Equal(2560, l2.PositionX); // d2 moved to 2560 to eliminate overlap
        Assert.Equal(2560, l1.TargetWidth);
        Assert.Equal(1920, l2.TargetWidth);
    }

    [Fact]
    public void OverlappingDisplays_PreservesSpatialLeftToRightOrdering()
    {
        // d2 is physically placed to the left of d1 (X = -1920), d1 is primary at X = 0
        var d1 = new DisplayInfo(@"\\.\DISPLAY1", "Display 1", "Model 1", true, 0, 0, new DisplayMode(1920, 1080, 60, false), 0, 0, ConnectionType.Hdmi);
        var d2 = new DisplayInfo(@"\\.\DISPLAY2", "Display 2", "Model 2", false, -1920, 0, new DisplayMode(1920, 1080, 60, false), 0, 0, ConnectionType.DisplayPort);

        // d2 resolution increases to 2560x1440, causing an overlap with d1 at X=0
        var upgradeMode = new DisplayMode(2560, 1440, 60, false);
        var layouts = WindowsDisplayService.CalculateLayout(new[] { d1, d2 }, d2.Id, upgradeMode);

        Assert.Equal(2, layouts.Count);
        var l1 = layouts.Single(l => l.DisplayId == d1.Id);
        var l2 = layouts.Single(l => l.DisplayId == d2.Id);

        // d2 is left of d1, so d2 should start at -2560 and d1 should be at 0 (primary)
        Assert.Equal(0, l1.PositionX);
        Assert.Equal(-2560, l2.PositionX);
    }
}
