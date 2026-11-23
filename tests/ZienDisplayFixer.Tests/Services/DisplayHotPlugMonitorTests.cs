namespace ZienDisplayFixer.Tests.Services;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZienDisplayFixer.App.Services;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public class DisplayHotPlugMonitorTests
{
    private class FakeDisplayService : IDisplayService
    {
        public List<DisplayInfo> Displays { get; set; } = new();
        public Dictionary<string, List<DisplayMode>> Modes { get; set; } = new();
        public List<(string DisplayId, DisplayMode Mode)> TestedModes { get; } = new();
        public List<(string DisplayId, DisplayMode Mode)> AppliedModes { get; } = new();
        public bool TestModeResult { get; set; } = true;
        public bool ApplyModeResult { get; set; } = true;
        public bool RestorePreviousResult { get; set; } = true;

        public event EventHandler? DisplaysChanged;

        public void RaiseDisplaysChanged() => DisplaysChanged?.Invoke(this, EventArgs.Empty);

        public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DisplayInfo>>(Displays);

        public Task<IReadOnlyList<DisplayMode>> GetModesAsync(string displayId, CancellationToken ct = default)
        {
            if (Modes.TryGetValue(displayId, out var modes))
            {
                return Task.FromResult<IReadOnlyList<DisplayMode>>(modes);
            }
            return Task.FromResult<IReadOnlyList<DisplayMode>>(Array.Empty<DisplayMode>());
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
            => Task.FromResult(RestorePreviousResult);
    }

    private class FakeSettingsService : ISettingsService
    {
        public AppSettings CurrentSettings { get; set; } = new();
        public event EventHandler<AppSettings>? SettingsChanged;

        public Task<AppSettings> LoadSettingsAsync(CancellationToken ct = default)
            => Task.FromResult(CurrentSettings);

        public Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default)
        {
            CurrentSettings = settings;
            SettingsChanged?.Invoke(this, settings);
            return Task.CompletedTask;
        }
    }

    private static (FakeDisplayService displayService, FakeSettingsService settingsService) CreateSetup(
        bool autoFix = true,
        bool needImprovement = true)
    {
        var displayService = new FakeDisplayService();
        var settingsService = new FakeSettingsService
        {
            CurrentSettings = new AppSettings { FixAutomaticallyOnConnect = autoFix }
        };

        var displayId = "disp-1";
        var currentMode = needImprovement
            ? new DisplayMode(1280, 720, 30.0)
            : new DisplayMode(1920, 1080, 60.0);

        var display = new DisplayInfo
        {
            Id = displayId,
            Name = "Test Monitor",
            Model = "TM-100",
            IsPrimary = true,
            CurrentMode = currentMode,
            PhysicalWidthMm = 531,
            PhysicalHeightMm = 298
        };

        displayService.Displays.Add(display);
        displayService.Modes[displayId] = new List<DisplayMode>
        {
            new DisplayMode(1280, 720, 30.0),
            new DisplayMode(1920, 1080, 60.0)
        };

        return (displayService, settingsService);
    }

    [Fact]
    public async Task HandleHotPlugAsync_WhenFixAutomaticallyOnConnectIsFalse_DoesNotApplyModes()
    {
        var (displayService, settingsService) = CreateSetup(autoFix: false, needImprovement: true);
        using var monitor = new DisplayHotPlugMonitor(displayService, settingsService);

        await monitor.HandleHotPlugAsync(TestContext.Current.CancellationToken);

        Assert.Empty(displayService.TestedModes);
        Assert.Empty(displayService.AppliedModes);
    }

    [Fact]
    public async Task HandleHotPlugAsync_WhenFixAutomaticallyOnConnectIsTrue_AppliesOptimalMode()
    {
        var (displayService, settingsService) = CreateSetup(autoFix: true, needImprovement: true);
        using var monitor = new DisplayHotPlugMonitor(displayService, settingsService);

        await monitor.HandleHotPlugAsync(TestContext.Current.CancellationToken);

        Assert.Single(displayService.TestedModes);
        Assert.Single(displayService.AppliedModes);
        Assert.Equal(1920, displayService.AppliedModes[0].Mode.Width);
        Assert.Equal(1080, displayService.AppliedModes[0].Mode.Height);
        Assert.Equal(60.0, displayService.AppliedModes[0].Mode.RefreshHz);
    }

    [Fact]
    public async Task HandleHotPlugAsync_WhenDisplaysAlreadyOptimal_DoesNotApplyModes()
    {
        var (displayService, settingsService) = CreateSetup(autoFix: true, needImprovement: false);
        using var monitor = new DisplayHotPlugMonitor(displayService, settingsService);

        await monitor.HandleHotPlugAsync(TestContext.Current.CancellationToken);

        Assert.Empty(displayService.TestedModes);
        Assert.Empty(displayService.AppliedModes);
    }

    [Fact]
    public async Task Debounce_CollapsesMultipleRapidEventsIntoSingleExecution()
    {
        var (displayService, settingsService) = CreateSetup(autoFix: true, needImprovement: true);
        using var monitor = new DisplayHotPlugMonitor(displayService, settingsService)
        {
            DebounceDelayMs = 50
        };

        displayService.RaiseDisplaysChanged();
        displayService.RaiseDisplaysChanged();
        displayService.RaiseDisplaysChanged();

        // Wait for debounce period to finish
        await Task.Delay(150, TestContext.Current.CancellationToken);

        Assert.Single(displayService.AppliedModes);
    }

    [Fact]
    public async Task Dispose_CancelsPendingDebounceExecution()
    {
        var (displayService, settingsService) = CreateSetup(autoFix: true, needImprovement: true);
        var monitor = new DisplayHotPlugMonitor(displayService, settingsService)
        {
            DebounceDelayMs = 150
        };

        displayService.RaiseDisplaysChanged();
        monitor.Dispose();

        await Task.Delay(250, TestContext.Current.CancellationToken);

        Assert.Empty(displayService.AppliedModes);
    }
}
