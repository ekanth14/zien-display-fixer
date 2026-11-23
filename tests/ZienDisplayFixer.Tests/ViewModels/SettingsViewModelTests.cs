namespace ZienDisplayFixer.Tests.ViewModels;

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZienDisplayFixer.App.Services;
using ZienDisplayFixer.App.ViewModels;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public class SettingsViewModelTests
{
    private class FakeSettingsService : ISettingsService
    {
        public AppSettings CurrentSettings { get; set; } = new();
        public int SaveCallCount { get; private set; }
        public AppSettings? LastSavedSettings { get; private set; }
        public event EventHandler<AppSettings>? SettingsChanged;

        public Task<AppSettings> LoadSettingsAsync(CancellationToken ct = default)
        {
            return Task.FromResult(CurrentSettings);
        }

        public Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default)
        {
            SaveCallCount++;
            LastSavedSettings = settings;
            CurrentSettings = settings;
            SettingsChanged?.Invoke(this, settings);
            return Task.CompletedTask;
        }

        public void TriggerExternalSettingsChange(AppSettings settings)
        {
            CurrentSettings = settings;
            SettingsChanged?.Invoke(this, settings);
        }
    }

    [Fact]
    public void InitialState_FromSettingsService_PopulatesPropertiesAndSyncsState()
    {
        var settings = new AppSettings
        {
            FixAutomaticallyOnConnect = false,
            StartAtLogin = true,
            IsAnimationEnabled = false
        };

        var service = new FakeSettingsService { CurrentSettings = settings };
        var state = new DisplaySetupState();

        var vm = new SettingsViewModel(service, state);

        Assert.False(vm.FixAutomaticallyOnConnect);
        Assert.True(vm.StartAtLogin);
        Assert.False(vm.IsAnimationEnabled);

        // State is synchronized with initial settings
        Assert.False(state.IsAnimationEnabled);
    }

    [Fact]
    public async Task LoadSettingsAsync_UpdatesViewModelAndSyncsState()
    {
        var service = new FakeSettingsService
        {
            CurrentSettings = new AppSettings
            {
                FixAutomaticallyOnConnect = false,
                StartAtLogin = true,
                IsAnimationEnabled = false
            }
        };
        var state = new DisplaySetupState();
        var vm = new SettingsViewModel(service, state);

        // Change service settings to new values
        service.CurrentSettings = new AppSettings
        {
            FixAutomaticallyOnConnect = true,
            StartAtLogin = false,
            IsAnimationEnabled = true
        };

        await vm.LoadSettingsAsync(TestContext.Current.CancellationToken);

        Assert.True(vm.FixAutomaticallyOnConnect);
        Assert.False(vm.StartAtLogin);
        Assert.True(vm.IsAnimationEnabled);
        Assert.True(state.IsAnimationEnabled);
    }

    [Fact]
    public void PropertyChange_TriggersSaveSettings_AndUpdatesStatusMessage()
    {
        var service = new FakeSettingsService();
        var state = new DisplaySetupState();
        var vm = new SettingsViewModel(service, state);

        var initialSaves = service.SaveCallCount;

        vm.FixAutomaticallyOnConnect = false;
        Assert.True(service.SaveCallCount > initialSaves);
        Assert.False(service.LastSavedSettings?.FixAutomaticallyOnConnect);
        Assert.Equal("Settings are saved automatically.", vm.StatusMessage);

        var countBeforeLogin = service.SaveCallCount;
        vm.StartAtLogin = true;
        Assert.True(service.SaveCallCount > countBeforeLogin);
        Assert.True(service.LastSavedSettings?.StartAtLogin);

        var countBeforeAnimation = service.SaveCallCount;
        vm.IsAnimationEnabled = false;
        Assert.True(service.SaveCallCount > countBeforeAnimation);
        Assert.False(service.LastSavedSettings?.IsAnimationEnabled);
        Assert.False(state.IsAnimationEnabled);
    }

    [Fact]
    public void SetupState_ExternalChange_SyncsBackToViewModelAndSaves()
    {
        var service = new FakeSettingsService();
        var state = new DisplaySetupState();
        var vm = new SettingsViewModel(service, state);

        state.IsAnimationEnabled = false;
        Assert.False(vm.IsAnimationEnabled);
        Assert.False(service.LastSavedSettings?.IsAnimationEnabled);
    }

    [Fact]
    public void Service_ExternalSettingsChange_SyncsToViewModelAndSetupState()
    {
        var service = new FakeSettingsService();
        var state = new DisplaySetupState();
        var vm = new SettingsViewModel(service, state);

        service.TriggerExternalSettingsChange(new AppSettings
        {
            FixAutomaticallyOnConnect = false,
            StartAtLogin = true,
            IsAnimationEnabled = false
        });

        Assert.False(vm.FixAutomaticallyOnConnect);
        Assert.True(vm.StartAtLogin);
        Assert.False(vm.IsAnimationEnabled);
        Assert.False(state.IsAnimationEnabled);
    }
}
