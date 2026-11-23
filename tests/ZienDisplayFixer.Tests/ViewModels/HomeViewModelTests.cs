using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZienDisplayFixer.App.ViewModels;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

namespace ZienDisplayFixer.Tests.ViewModels;

public class HomeViewModelTests
{
    private class FakeDisplayService : IDisplayService
    {
        public List<DisplayInfo> Displays { get; set; } = [];
        public Dictionary<string, IReadOnlyList<DisplayMode>> Modes { get; set; } = new();
        public bool TestModeResult { get; set; } = true;
        public bool ApplyModeResult { get; set; } = true;
        public List<(string DisplayId, DisplayMode Mode)> TestedModes { get; } = [];
        public List<(string DisplayId, DisplayMode Mode)> AppliedModes { get; } = [];
        public bool RestorePreviousModesCalled { get; private set; }

        public event EventHandler? DisplaysChanged;

        public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<DisplayInfo>>(Displays.ToList());
        }

        public Task<IReadOnlyList<DisplayMode>> GetModesAsync(string displayId, CancellationToken ct = default)
        {
            if (Modes.TryGetValue(displayId, out var modes))
            {
                return Task.FromResult(modes);
            }
            return Task.FromResult<IReadOnlyList<DisplayMode>>([]);
        }

        public Task<bool> TestModeAsync(string displayId, DisplayMode mode, CancellationToken ct = default)
        {
            TestedModes.Add((displayId, mode));
            return Task.FromResult(TestModeResult);
        }

        public Task<bool> ApplyModeAsync(string displayId, DisplayMode mode, CancellationToken ct = default)
        {
            AppliedModes.Add((displayId, mode));
            return Task.FromResult(ApplyModeResult);
        }

        public Task<bool> RestorePreviousModesAsync(CancellationToken ct = default)
        {
            RestorePreviousModesCalled = true;
            return Task.FromResult(true);
        }

        public void TriggerDisplaysChanged()
        {
            DisplaysChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private class FakeEdidReader : IEdidReader
    {
        public Dictionary<string, byte[]> Edids { get; set; } = new();

        public Task<byte[]?> ReadRawEdidAsync(string displayId, CancellationToken ct = default)
        {
            Edids.TryGetValue(displayId, out var data);
            return Task.FromResult(data);
        }

        public Task<IReadOnlyDictionary<string, byte[]>> ReadAllEdidsAsync(CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyDictionary<string, byte[]>>(Edids);
        }
    }

    private class FakeScreenModeService : IScreenModeService
    {
        public ScreenTopology CurrentTopology { get; set; } = ScreenTopology.Extend;
        public List<ScreenTopology> SetTopologyCalls { get; } = [];

        public Task<ScreenTopology> GetCurrentTopologyAsync(CancellationToken ct = default)
        {
            return Task.FromResult(CurrentTopology);
        }

        public Task SetTopologyAsync(ScreenTopology topology, CancellationToken ct = default)
        {
            CurrentTopology = topology;
            SetTopologyCalls.Add(topology);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task StatusText_WhenNoExternalDisplays_ShowsNoExternalDisplaysConnected()
    {
        var displayService = new FakeDisplayService
        {
            Displays =
            [
                new DisplayInfo(
                    id: "builtin",
                    name: "Internal Display",
                    model: "Internal Display",
                    isPrimary: true,
                    positionX: 0,
                    positionY: 0,
                    currentMode: new DisplayMode(1920, 1080, 60.0, false),
                    physicalWidthMm: 310,
                    physicalHeightMm: 174,
                    connection: ConnectionType.Unknown)
            ]
        };

        var edidReader = new FakeEdidReader();
        var screenMode = new FakeScreenModeService();
        var vm = new HomeViewModel(displayService, edidReader, screenMode)
        {
            DeviceType = DeviceType.Laptop
        };

        await vm.LoadDisplaysAsync(TestContext.Current.CancellationToken);

        Assert.Equal("No external displays connected", vm.StatusText);
    }

    [Fact]
    public async Task StatusText_WhenDisplaysCanBeImproved_ShowsCorrectCount()
    {
        var displayService = new FakeDisplayService
        {
            Displays =
            [
                new DisplayInfo(
                    id: "disp1",
                    name: "Dell U2720Q",
                    model: "Dell U2720Q",
                    isPrimary: true,
                    positionX: 0,
                    positionY: 0,
                    currentMode: new DisplayMode(1920, 1080, 60.0, false), // Suboptimal (native is 4K)
                    physicalWidthMm: 597,
                    physicalHeightMm: 336,
                    connection: ConnectionType.DisplayPort),
                new DisplayInfo(
                    id: "disp2",
                    name: "LG 27GL850",
                    model: "LG 27GL850",
                    isPrimary: false,
                    positionX: 1920,
                    positionY: 0,
                    currentMode: new DisplayMode(2560, 1440, 144.0, false), // Optimal
                    physicalWidthMm: 597,
                    physicalHeightMm: 336,
                    connection: ConnectionType.DisplayPort)
            ],
            Modes = new()
            {
                ["disp1"] =
                [
                    new DisplayMode(1920, 1080, 60.0, false),
                    new DisplayMode(3840, 2160, 60.0, false)
                ],
                ["disp2"] =
                [
                    new DisplayMode(1920, 1080, 60.0, false),
                    new DisplayMode(2560, 1440, 144.0, false)
                ]
            }
        };

        var edidReader = new FakeEdidReader();
        var screenMode = new FakeScreenModeService();
        var vm = new HomeViewModel(displayService, edidReader, screenMode);

        await vm.LoadDisplaysAsync(TestContext.Current.CancellationToken);

        Assert.Equal("1 of 2 displays can be improved", vm.StatusText);
    }

    [Fact]
    public async Task FixScreensCommand_WhenTestModeFails_SetsErrorMessageAndAborts()
    {
        var displayService = new FakeDisplayService
        {
            Displays =
            [
                new DisplayInfo(
                    id: "disp1",
                    name: "Monitor",
                    model: "Monitor",
                    isPrimary: true,
                    positionX: 0,
                    positionY: 0,
                    currentMode: new DisplayMode(1920, 1080, 60.0, false),
                    physicalWidthMm: 500,
                    physicalHeightMm: 300,
                    connection: ConnectionType.Hdmi)
            ],
            Modes = new()
            {
                ["disp1"] =
                [
                    new DisplayMode(1920, 1080, 60.0, false),
                    new DisplayMode(2560, 1440, 60.0, false)
                ]
            },
            TestModeResult = false // Driver rejected!
        };

        var edidReader = new FakeEdidReader();
        var screenMode = new FakeScreenModeService();
        var vm = new HomeViewModel(displayService, edidReader, screenMode);

        await vm.LoadDisplaysAsync(TestContext.Current.CancellationToken);
        await vm.FixScreensCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("rejected by display driver", vm.ErrorMessage);
        Assert.Empty(displayService.AppliedModes);
        Assert.False(vm.IsConfirmationVisible);
    }

    [Fact]
    public async Task FixScreensCommand_WhenModesPass_AppliesModesAndStartsConfirmation()
    {
        var displayService = new FakeDisplayService
        {
            Displays =
            [
                new DisplayInfo(
                    id: "disp1",
                    name: "Monitor",
                    model: "Monitor",
                    isPrimary: true,
                    positionX: 0,
                    positionY: 0,
                    currentMode: new DisplayMode(1920, 1080, 60.0, false),
                    physicalWidthMm: 500,
                    physicalHeightMm: 300,
                    connection: ConnectionType.Hdmi)
            ],
            Modes = new()
            {
                ["disp1"] =
                [
                    new DisplayMode(1920, 1080, 60.0, false),
                    new DisplayMode(2560, 1440, 60.0, false)
                ]
            },
            TestModeResult = true,
            ApplyModeResult = true
        };

        var edidReader = new FakeEdidReader();
        var screenMode = new FakeScreenModeService();
        var vm = new HomeViewModel(displayService, edidReader, screenMode);

        await vm.LoadDisplaysAsync(TestContext.Current.CancellationToken);
        await vm.FixScreensCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        Assert.Single(displayService.AppliedModes);
        Assert.Equal(2560, displayService.AppliedModes[0].Mode.Width);
        Assert.True(vm.IsConfirmationVisible);
        Assert.NotNull(vm.Confirmation);
        Assert.Equal(12, vm.Confirmation.RemainingSeconds);

        // Keep confirmation
        vm.Confirmation.KeepCommand.Execute(null);
        Assert.False(vm.IsConfirmationVisible);
    }

    [Fact]
    public async Task Confirmation_OnRevert_RestoresPreviousModesAndReloads()
    {
        var displayService = new FakeDisplayService
        {
            Displays =
            [
                new DisplayInfo(
                    id: "disp1",
                    name: "Monitor",
                    model: "Monitor",
                    isPrimary: true,
                    positionX: 0,
                    positionY: 0,
                    currentMode: new DisplayMode(1920, 1080, 60.0, false),
                    physicalWidthMm: 500,
                    physicalHeightMm: 300,
                    connection: ConnectionType.Hdmi)
            ],
            Modes = new()
            {
                ["disp1"] =
                [
                    new DisplayMode(1920, 1080, 60.0, false),
                    new DisplayMode(2560, 1440, 60.0, false)
                ]
            }
        };

        var edidReader = new FakeEdidReader();
        var screenMode = new FakeScreenModeService();
        var vm = new HomeViewModel(displayService, edidReader, screenMode);

        await vm.LoadDisplaysAsync(TestContext.Current.CancellationToken);
        await vm.FixScreensCommand.ExecuteAsync(null);

        Assert.True(vm.IsConfirmationVisible);
        Assert.NotNull(vm.Confirmation);

        // User reverts or countdown expires
        vm.Confirmation.RevertCommand.Execute(null);

        Assert.False(vm.IsConfirmationVisible);
        Assert.True(displayService.RestorePreviousModesCalled);
    }

    [Fact]
    public async Task SetTopologyCommand_InvokesScreenModeServiceAndUpdatesState()
    {
        var displayService = new FakeDisplayService();
        var edidReader = new FakeEdidReader();
        var screenMode = new FakeScreenModeService();
        var vm = new HomeViewModel(displayService, edidReader, screenMode);

        await vm.SetTopologyCommand.ExecuteAsync(ScreenTopology.Duplicate);

        Assert.Equal(ScreenTopology.Duplicate, vm.CurrentTopology);
        Assert.Single(screenMode.SetTopologyCalls);
        Assert.Equal(ScreenTopology.Duplicate, screenMode.SetTopologyCalls[0]);
    }
}
