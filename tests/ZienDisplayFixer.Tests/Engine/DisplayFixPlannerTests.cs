using Xunit;
using ZienDisplayFixer.App.Services.Windows;
using ZienDisplayFixer.Core.Engine;
using ZienDisplayFixer.Core.Models;

namespace ZienDisplayFixer.Tests.Engine;

public class DisplayFixPlannerTests
{
    private static DisplayInfo Display(string id, bool primary, int x, int y, DisplayMode mode, string hardwareId = "") =>
        new()
        {
            Id = id,
            Name = id,
            HardwareId = hardwareId,
            IsPrimary = primary,
            PositionX = x,
            PositionY = y,
            CurrentMode = mode
        };

    // Laptop panel + a TV that only does 4K at 24/30 Hz + a USB-C hub limited to 720p.
    private static (List<DisplayInfo> Displays, Dictionary<string, IReadOnlyList<DisplayMode>> Modes) LaptopTvHubSetup()
    {
        var displays = new List<DisplayInfo>
        {
            Display(@"\\.\DISPLAY3", true, 0, 0, new DisplayMode(2880, 1800, 120), "SDC41AA"),
            Display(@"\\.\DISPLAY2", false, -561, -2160, new DisplayMode(4096, 2160, 24), "HEC0030"),
            Display(@"\\.\DISPLAY1", false, 2880, 0, new DisplayMode(1280, 720, 60), "ACR0358"),
        };

        var modes = new Dictionary<string, IReadOnlyList<DisplayMode>>
        {
            [@"\\.\DISPLAY3"] = [new(2880, 1800, 120), new(2880, 1800, 60), new(1920, 1080, 120)],
            [@"\\.\DISPLAY2"] =
            [
                new(4096, 2160, 24), new(3840, 2160, 30), new(3840, 2160, 24),
                new(1920, 1080, 60), new(1920, 1080, 30), new(1280, 720, 60)
            ],
            [@"\\.\DISPLAY1"] = [new(1280, 720, 60), new(1024, 768, 60), new(800, 600, 60)],
        };

        return (displays, modes);
    }

    [Fact]
    public void BestModePicker_PrefersSixtyHertzOverLowRefresh4K()
    {
        var (displays, modes) = LaptopTvHubSetup();

        var best = BestModePicker.PickBestMode(modes[@"\\.\DISPLAY2"], displays[1]);

        Assert.Equal(new DisplayMode(1920, 1080, 60), best);
    }

    [Fact]
    public void BestModePicker_FallsBackToLowRefresh_WhenNothingReachesSixtyHertz()
    {
        var display = Display("tv", false, 0, 0, new DisplayMode(1920, 1080, 30));
        DisplayMode[] modes = [new(1920, 1080, 30), new(3840, 2160, 30), new(1280, 720, 25)];

        var best = BestModePicker.PickBestMode(modes, display);

        Assert.Equal(new DisplayMode(3840, 2160, 30), best);
    }

    [Fact]
    public void BestModePicker_PrefersPanelAspect_OverWiderCinemaMode()
    {
        var display = Display("tv", false, 0, 0, new DisplayMode(1920, 1080, 60)) with
        {
            PhysicalWidthMm = 1210,
            PhysicalHeightMm = 680
        };
        DisplayMode[] modes = [new(4096, 2160, 60), new(3840, 2160, 60)];

        var best = BestModePicker.PickBestMode(modes, display);

        Assert.Equal(new DisplayMode(3840, 2160, 60), best);
    }

    [Fact]
    public void Plan_LaptopTvHub_SwitchesTvTo1080p60AndCentresItAboveLaptop()
    {
        var (displays, modes) = LaptopTvHubSetup();

        var plan = DisplayFixPlanner.Plan(displays, modes);
        var byId = plan.Targets.ToDictionary(t => t.DisplayId);

        Assert.True(plan.HasChanges);
        Assert.Equal(1, plan.ModeChangeCount);

        var laptop = byId[@"\\.\DISPLAY3"];
        Assert.False(laptop.ModeChanged);
        Assert.Equal((0, 0), (laptop.PositionX, laptop.PositionY));

        var tv = byId[@"\\.\DISPLAY2"];
        Assert.True(tv.ModeChanged);
        Assert.Equal(new DisplayMode(1920, 1080, 60), tv.Mode);
        Assert.Equal((480, -1080), (tv.PositionX, tv.PositionY));

        var hub = byId[@"\\.\DISPLAY1"];
        Assert.False(hub.ModeChanged);
        Assert.False(hub.PositionChanged);
        Assert.Equal((2880, 0), (hub.PositionX, hub.PositionY));
    }

    [Fact]
    public void Plan_IsIdempotent_AfterFixIsApplied()
    {
        var (displays, modes) = LaptopTvHubSetup();
        var first = DisplayFixPlanner.Plan(displays, modes);

        var applied = displays
            .Select(d =>
            {
                var t = first.Targets.Single(t => t.DisplayId == d.Id);
                return d with { CurrentMode = t.Mode, PositionX = t.PositionX, PositionY = t.PositionY };
            })
            .ToList();

        var second = DisplayFixPlanner.Plan(applied, modes);

        Assert.False(second.HasChanges);
    }

    [Fact]
    public void Plan_KeepsUserLayout_WhenModesAreOptimalAndLayoutIsConnected()
    {
        var displays = new List<DisplayInfo>
        {
            Display("main", true, 0, 0, new DisplayMode(1920, 1080, 60)),
            Display("side", false, 1920, 300, new DisplayMode(1920, 1080, 60)),
        };
        var modes = new Dictionary<string, IReadOnlyList<DisplayMode>>
        {
            ["main"] = [new(1920, 1080, 60)],
            ["side"] = [new(1920, 1080, 60)],
        };

        var plan = DisplayFixPlanner.Plan(displays, modes);

        Assert.False(plan.HasChanges);
    }

    [Fact]
    public void Plan_ReconnectsDetachedDisplay_EvenWithoutModeChanges()
    {
        var displays = new List<DisplayInfo>
        {
            Display("main", true, 0, 0, new DisplayMode(1920, 1080, 60)),
            Display("floating", false, 2500, 0, new DisplayMode(1920, 1080, 60)),
        };
        var modes = new Dictionary<string, IReadOnlyList<DisplayMode>>
        {
            ["main"] = [new(1920, 1080, 60)],
            ["floating"] = [new(1920, 1080, 60)],
        };

        var plan = DisplayFixPlanner.Plan(displays, modes);
        var floating = plan.Targets.Single(t => t.DisplayId == "floating");

        Assert.True(plan.HasChanges);
        Assert.Equal(0, plan.ModeChangeCount);
        Assert.Equal((1920, 0), (floating.PositionX, floating.PositionY));
    }

    [Fact]
    public void Plan_SavedPlacement_OverridesCurrentPosition()
    {
        var displays = new List<DisplayInfo>
        {
            Display("laptop", true, 0, 0, new DisplayMode(2880, 1800, 120), "SDC41AA"),
            Display("tv", false, 2880, 0, new DisplayMode(1920, 1080, 60), "HEC0030"),
        };
        var modes = new Dictionary<string, IReadOnlyList<DisplayMode>>
        {
            ["laptop"] = [new(2880, 1800, 120)],
            ["tv"] = [new(1920, 1080, 60)],
        };
        var saved = new Dictionary<string, DisplayPlacement> { ["HEC0030"] = DisplayPlacement.Above };

        var plan = DisplayFixPlanner.Plan(displays, modes, saved);
        var tv = plan.Targets.Single(t => t.DisplayId == "tv");

        Assert.Equal((480, -1080), (tv.PositionX, tv.PositionY));
        Assert.True(tv.PositionChanged);
    }

    [Fact]
    public void Plan_IgnoresSavedPlacement_ForIdenticalMonitors()
    {
        var displays = new List<DisplayInfo>
        {
            Display("main", true, 0, 0, new DisplayMode(1920, 1080, 60), "MAIN001"),
            Display("left", false, -1920, 0, new DisplayMode(1920, 1080, 60), "DEL4321"),
            Display("right", false, 1920, 0, new DisplayMode(1920, 1080, 60), "DEL4321"),
        };
        var modes = displays.ToDictionary(d => d.Id, _ => (IReadOnlyList<DisplayMode>)[new DisplayMode(1920, 1080, 60)]);
        var saved = new Dictionary<string, DisplayPlacement> { ["DEL4321"] = DisplayPlacement.Above };

        var plan = DisplayFixPlanner.Plan(displays, modes, saved);

        Assert.False(plan.HasChanges);
    }

    [Fact]
    public void Arrange_StacksMultipleRightDisplaysOutward()
    {
        ArrangeInput[] inputs =
        [
            new("main", 1920, 1080, true, 0, 0),
            new("far", 1280, 1024, false, 4000, 0),
            new("near", 2560, 1440, false, 1920, 0),
        ];

        var positions = DisplayArranger.Arrange(inputs);

        Assert.Equal((1920, 0), positions["near"]);
        Assert.Equal((4480, 0), positions["far"]);
        Assert.True(DisplayArranger.IsLayoutHealthy(
            inputs.Select(i => i with { CurrentX = positions[i.Id].X, CurrentY = positions[i.Id].Y }).ToList()));
    }

    [Fact]
    public void Arrange_PlacesLeftDisplaysFlushAgainstPrimary()
    {
        ArrangeInput[] inputs =
        [
            new("main", 1920, 1080, true, 0, 0),
            new("left", 1366, 768, false, -3000, 500),
        ];

        var positions = DisplayArranger.Arrange(inputs);

        Assert.Equal((-1366, 0), positions["left"]);
    }

    [Theory]
    [InlineData(@"MONITOR\HEC0030\{4d36e96e-e325-11ce-bfc1-08002be10318}\0006", "HEC0030")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("MONITOR", "")]
    public void ParseHardwareId_ExtractsPnpId(string? deviceId, string expected)
    {
        Assert.Equal(expected, WindowsDisplayService.ParseHardwareId(deviceId));
    }
}
