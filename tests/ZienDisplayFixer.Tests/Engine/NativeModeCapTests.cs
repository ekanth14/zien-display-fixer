using Xunit;
using ZienDisplayFixer.App.Services.Windows;
using ZienDisplayFixer.Core.Engine;
using ZienDisplayFixer.Core.Models;

namespace ZienDisplayFixer.Tests.Engine;

public class NativeModeCapTests
{
    private static DisplayInfo Display(DisplayMode? native, double widthMm = 0, double heightMm = 0) =>
        new()
        {
            Id = "disp",
            NativeMode = native,
            PhysicalWidthMm = widthMm,
            PhysicalHeightMm = heightMm,
            CurrentMode = new DisplayMode(1024, 768, 60)
        };

    [Fact]
    public void VgaAdapter_AdvertisingFullHd_StaysAtMonitorNative()
    {
        // Mode list a USB-C VGA adapter reported for an Acer S190HQL (native 1360x768 after override).
        DisplayMode[] modes =
        [
            new(1920, 1080, 60), new(1920, 1080, 50), new(1920, 1080, 59), new(1280, 720, 60),
            new(1360, 768, 60), new(1280, 800, 60), new(1280, 768, 60), new(1024, 768, 60), new(800, 600, 75)
        ];

        var best = BestModePicker.PickBestMode(modes, Display(new DisplayMode(1360, 768, 60.02), 410, 230));

        Assert.Equal(new DisplayMode(1360, 768, 60), best);
    }

    [Fact]
    public void Monitor_WithSmallNative_IgnoresTallerAndWiderModes()
    {
        DisplayMode[] modes =
        [
            new(1920, 1080, 60), new(1280, 1024, 60), new(1280, 720, 60), new(1280, 720, 50), new(1024, 768, 60)
        ];

        var best = BestModePicker.PickBestMode(modes, Display(new DisplayMode(1280, 720, 60), 1150, 650));

        Assert.Equal(new DisplayMode(1280, 720, 60), best);
    }

    [Fact]
    public void Tv_WithFullHdNative_DoesNotJumpTo4K()
    {
        DisplayMode[] modes =
        [
            new(4096, 2160, 24), new(3840, 2160, 30), new(1920, 1080, 60), new(1920, 1080, 50), new(1280, 720, 60)
        ];

        var best = BestModePicker.PickBestMode(modes, Display(new DisplayMode(1920, 1080, 60), 1150, 650));

        Assert.Equal(new DisplayMode(1920, 1080, 60), best);
    }

    [Fact]
    public void LaptopPanel_UsesNativeAtHighestRefresh()
    {
        DisplayMode[] modes = [new(2880, 1800, 60), new(2880, 1800, 120), new(2560, 1600, 120), new(1920, 1200, 120)];

        var best = BestModePicker.PickBestMode(modes, Display(new DisplayMode(2880, 1800, 120), 300, 190));

        Assert.Equal(new DisplayMode(2880, 1800, 120), best);
    }

    [Fact]
    public void NativeOnlyAtThirtyHertz_FallsBackToSmoothModeBelowNative()
    {
        DisplayMode[] modes = [new(3840, 2160, 30), new(3840, 2160, 24), new(2560, 1440, 60), new(1920, 1080, 60)];

        var best = BestModePicker.PickBestMode(modes, Display(new DisplayMode(3840, 2160, 30)));

        Assert.Equal(new DisplayMode(2560, 1440, 60), best);
    }

    [Fact]
    public void UnknownNative_KeepsPreviousBehaviour()
    {
        DisplayMode[] modes = [new(3840, 2160, 60), new(1920, 1080, 60)];

        var best = BestModePicker.PickBestMode(modes, Display(native: null));

        Assert.Equal(new DisplayMode(3840, 2160, 60), best);
    }

    [Fact]
    public void Plan_FourScreenDesk_KeepsEveryScreenAtNativeWhenAlreadyConfigured()
    {
        var displays = new List<DisplayInfo>
        {
            new() { Id = "laptop", IsPrimary = true, CurrentMode = new(2880, 1800, 120), NativeMode = new(2880, 1800, 120) },
            new() { Id = "acer", PositionX = -1360, CurrentMode = new(1360, 768, 60), NativeMode = new(1360, 768, 60.02) },
            new() { Id = "philips", PositionX = 2880, CurrentMode = new(1280, 720, 60), NativeMode = new(1280, 720, 60) },
            new() { Id = "tv", PositionX = 480, PositionY = -1080, CurrentMode = new(1920, 1080, 60), NativeMode = new(1920, 1080, 60) },
        };
        var modes = new Dictionary<string, IReadOnlyList<DisplayMode>>
        {
            ["laptop"] = [new(2880, 1800, 120), new(2880, 1800, 60)],
            ["acer"] = [new(1920, 1080, 60), new(1360, 768, 60), new(1280, 720, 60)],
            ["philips"] = [new(1920, 1080, 60), new(1280, 1024, 60), new(1280, 720, 60)],
            ["tv"] = [new(4096, 2160, 24), new(3840, 2160, 30), new(1920, 1080, 60)],
        };

        var plan = DisplayFixPlanner.Plan(displays, modes);

        Assert.False(plan.HasChanges);
    }

    [Fact]
    public void Plan_TvGrowingTaller_StaysAboveInsteadOfJumpingRight()
    {
        // TV sits flush above the laptop at 1680x1050; growing it to 1080 rows must not be read as "overlapping, so right".
        var displays = new List<DisplayInfo>
        {
            new() { Id = "laptop", IsPrimary = true, CurrentMode = new(2880, 1800, 120), NativeMode = new(2880, 1800, 120) },
            new() { Id = "acer", PositionX = -1360, CurrentMode = new(1360, 768, 60), NativeMode = new(1360, 768, 60.02) },
            new() { Id = "philips", PositionX = 2880, CurrentMode = new(1280, 720, 60), NativeMode = new(1280, 720, 60) },
            new() { Id = "tv", PositionX = 480, PositionY = -1050, CurrentMode = new(1680, 1050, 60), NativeMode = new(1920, 1080, 60) },
        };
        var modes = new Dictionary<string, IReadOnlyList<DisplayMode>>
        {
            ["laptop"] = [new(2880, 1800, 120)],
            ["acer"] = [new(1360, 768, 60)],
            ["philips"] = [new(1280, 720, 60)],
            ["tv"] = [new(1920, 1080, 60), new(1680, 1050, 60)],
        };

        var plan = DisplayFixPlanner.Plan(displays, modes);
        var byId = plan.Targets.ToDictionary(t => t.DisplayId);

        Assert.Equal(new DisplayMode(1920, 1080, 60), byId["tv"].Mode);
        Assert.Equal((480, -1080), (byId["tv"].PositionX, byId["tv"].PositionY));
        Assert.Equal((2880, 0), (byId["philips"].PositionX, byId["philips"].PositionY));
        Assert.Equal((-1360, 0), (byId["acer"].PositionX, byId["acer"].PositionY));
    }

    [Theory]
    [InlineData(@"\\?\DISPLAY#ACR0358#4&1c5b15a4&0&UID24647#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}", @"DISPLAY\ACR0358\4&1c5b15a4&0&UID24647")]
    [InlineData(@"DISPLAY#HEC0030#4&1c5b15a4&0&UID4145#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}", @"DISPLAY\HEC0030\4&1c5b15a4&0&UID4145")]
    [InlineData(@"MONITOR\HEC0030\{4d36e96e-e325-11ce-bfc1-08002be10318}\0006", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void InstancePathFromInterfaceName_ParsesMonitorInterface(string? interfaceName, string? expected)
    {
        Assert.Equal(expected, WindowsMonitorEdid.InstancePathFromInterfaceName(interfaceName));
    }
}
