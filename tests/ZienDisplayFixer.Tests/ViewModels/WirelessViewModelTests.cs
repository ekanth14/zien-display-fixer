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
    public void Construction_WithDefaultConstructor_ProvidesCleanProductionState()
    {
        var vm = new WirelessViewModel();

        Assert.Equal("Wireless", vm.Title);
        Assert.True(vm.IsSupported);
        Assert.False(vm.IsScanning);
        Assert.Empty(vm.DiscoveredDevices);
        Assert.False(vm.HasDiscoveredDevices);
    }

    [Fact]
    public async Task SearchAgainCommand_InvokesScanNearby_AndPopulatesDevices()
    {
        var launcher = new FakeWirelessLauncher();
        var vm = new WirelessViewModel(launcher);

        await vm.SearchAgainCommand.ExecuteAsync(null);

        Assert.Equal(1, launcher.ScanCallCount);
        Assert.Equal(2, vm.DiscoveredDevices.Count);
        Assert.True(vm.HasDiscoveredDevices);
        Assert.Equal("Living Room TV", vm.DiscoveredDevices[0].Name);
        Assert.Equal("Bedroom TV", vm.DiscoveredDevices[1].Name);
    }

    [Fact]
    public async Task SearchAgainCommand_AutoOpenPicker_InvokesOpenPicker()
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
        await vm.SearchAgainCommand.ExecuteAsync(null);

        var first = vm.DiscoveredDevices[0];
        Assert.False(first.IsConnected);

        await vm.ConnectCommand.ExecuteAsync(first);

        Assert.True(first.IsConnected);
        Assert.Equal(1, launcher.OpenPickerCallCount);
        Assert.Contains(first.Name, vm.StatusMessage);
    }

    [Fact]
    public void IsSupported_WhenLauncherUnsupported_ReflectsFalse()
    {
        var launcher = new FakeWirelessLauncher { IsSupported = false };
        var vm = new WirelessViewModel(launcher);

        Assert.False(vm.IsSupported);
    }
}
