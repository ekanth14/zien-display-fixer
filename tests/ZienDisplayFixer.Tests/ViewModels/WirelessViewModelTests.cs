namespace ZienDisplayFixer.Tests.ViewModels;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZienDisplayFixer.App.Services;
using ZienDisplayFixer.App.ViewModels;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public class WirelessViewModelTests
{
    private class FakeWirelessLauncher : IWirelessDisplayLauncher
    {
        public bool IsSupported { get; set; } = true;
        public int OpenPickerCallCount { get; private set; }
        public int ScanCallCount { get; private set; }
        public List<WirelessDisplayDevice> DevicesToReturn { get; set; } =
        [
            new WirelessDisplayDevice("w-1", "Living Room TV", false, 55.0),
            new WirelessDisplayDevice("w-2", "Bedroom TV", false, 43.0)
        ];

        public Task OpenWirelessDisplayPickerAsync(CancellationToken ct = default)
        {
            OpenPickerCallCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WirelessDisplayDevice>> ScanNearbyAsync(CancellationToken ct = default)
        {
            ScanCallCount++;
            return Task.FromResult<IReadOnlyList<WirelessDisplayDevice>>(DevicesToReturn);
        }
    }

    [Fact]
    public void Construction_WithDefaultConstructor_ProvidesMockDevicesAndSupported()
    {
        var vm = new WirelessViewModel();

        Assert.Equal("Wireless", vm.Title);
        Assert.True(vm.IsSupported);
        Assert.False(vm.IsScanning);
        Assert.True(vm.DiscoveredDevices.Count > 0);
        Assert.True(vm.HasDiscoveredDevices);
    }

    [Fact]
    public void Construction_WithLauncher_ReflectsLauncherSupported()
    {
        var launcher = new FakeWirelessLauncher { IsSupported = false };
        var vm = new WirelessViewModel(launcher);

        Assert.False(vm.IsSupported);
    }

    [Fact]
    public async Task SearchAgainCommand_ExecutesScan_UpdatesDiscoveredDevicesAndStatus()
    {
        var launcher = new FakeWirelessLauncher();
        var vm = new WirelessViewModel(launcher);

        Assert.Equal(2, vm.DiscoveredDevices.Count);
        Assert.Equal(1, launcher.ScanCallCount);

        // Update launcher devices
        launcher.DevicesToReturn =
        [
            new WirelessDisplayDevice("w-1", "Living Room TV", false, 55.0),
            new WirelessDisplayDevice("w-2", "Bedroom TV", false, 43.0),
            new WirelessDisplayDevice("w-3", "Office TV", false, 65.0)
        ];

        await vm.SearchAgainCommand.ExecuteAsync(null);

        Assert.False(vm.IsScanning);
        Assert.Equal(3, vm.DiscoveredDevices.Count);
        Assert.Equal(2, launcher.ScanCallCount);
        Assert.Contains("3", vm.StatusMessage);
    }

    [Fact]
    public async Task SearchAgainCommand_WithOpenPickerParamOrAutoOpen_TriggersLauncherOpenPicker()
    {
        var launcher = new FakeWirelessLauncher();
        var vm = new WirelessViewModel(launcher);

        Assert.Equal(0, launcher.OpenPickerCallCount);

        // Pass true to open picker
        await vm.SearchAgainCommand.ExecuteAsync(true);
        Assert.Equal(1, launcher.OpenPickerCallCount);

        // Or set AutoOpenPickerOnSearch
        vm.AutoOpenPickerOnSearch = true;
        await vm.SearchAgainCommand.ExecuteAsync(null);
        Assert.Equal(2, launcher.OpenPickerCallCount);
    }

    [Fact]
    public async Task ConnectCommand_CallsOpenPicker_MarksConnected_AndUpdatesStatus()
    {
        var launcher = new FakeWirelessLauncher();
        var vm = new WirelessViewModel(launcher);

        var first = vm.DiscoveredDevices[0];
        Assert.False(first.IsConnected);

        await vm.ConnectCommand.ExecuteAsync(first);

        Assert.True(first.IsConnected);
        Assert.Equal(1, launcher.OpenPickerCallCount);
        Assert.Contains(first.Name, vm.StatusMessage);
    }

    [Fact]
    public async Task ConnectCommand_InDemoMode_AddsDisplayToSetupState()
    {
        var setupState = new DisplaySetupState();
        var launcher = new FakeWirelessLauncher();
        var vm = new WirelessViewModel(launcher, setupState);

        Assert.Empty(setupState.Displays);

        var device = vm.DiscoveredDevices[0];
        await vm.ConnectCommand.ExecuteAsync(device);

        Assert.Single(setupState.Displays);
        var added = setupState.Displays[0];
        Assert.Equal(device.Name, added.Name);
        Assert.Equal(device.Id, added.Id);
        Assert.Equal(ConnectionType.WiFi, added.Connection);
        Assert.True(added.PhysicalWidthMm > 0);
        Assert.True(added.PhysicalHeightMm > 0);
    }

    [Fact]
    public async Task ConnectCommand_ItemConnectCommand_AlsoConnectsDevice()
    {
        var setupState = new DisplaySetupState();
        var launcher = new FakeWirelessLauncher();
        var vm = new WirelessViewModel(launcher, setupState);

        var device = vm.DiscoveredDevices[1];
        Assert.NotNull(device.ConnectCommand);

        device.ConnectCommand.Execute(null);

        // Wait brief task completion
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.True(device.IsConnected);
        Assert.Equal(1, launcher.OpenPickerCallCount);
        Assert.Contains(setupState.Displays, d => d.Name == device.Name);
    }

    [Fact]
    public async Task OpenSystemSettingsCommand_InvokesPickerOnLauncher()
    {
        var launcher = new FakeWirelessLauncher();
        var vm = new WirelessViewModel(launcher);

        await vm.OpenSystemSettingsCommand.ExecuteAsync(null);

        Assert.Equal(1, launcher.OpenPickerCallCount);
        Assert.Contains("Opened", vm.StatusMessage);
    }

    [Fact]
    public void SetupState_ExternalChange_UpdatesDeviceConnectedStatus()
    {
        var setupState = new DisplaySetupState();
        var launcher = new FakeWirelessLauncher();
        var vm = new WirelessViewModel(launcher, setupState);

        var device = vm.DiscoveredDevices[0];
        Assert.False(device.IsConnected);

        // Add display directly to setup state
        setupState.Displays.Add(new DisplayInfo(
            id: device.Id,
            name: device.Name,
            model: device.Name,
            isPrimary: false,
            positionX: 0,
            positionY: 0,
            currentMode: new DisplayMode(3840, 2160, 60.0),
            physicalWidthMm: 1200,
            physicalHeightMm: 700,
            connection: ConnectionType.WiFi));

        // When setup changes event is triggered (e.g. state change)
        setupState.LaptopScreenSize = 16.0;

        Assert.True(device.IsConnected);
    }
}
