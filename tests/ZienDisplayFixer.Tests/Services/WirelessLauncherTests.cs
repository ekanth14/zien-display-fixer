namespace ZienDisplayFixer.Tests.Services;

using System;
using System.Threading.Tasks;
using Xunit;
using ZienDisplayFixer.App.Services.Linux;
using ZienDisplayFixer.App.Services.MacOS;
using ZienDisplayFixer.App.Services.Windows;
using ZienDisplayFixer.Core.Interfaces;

public class WirelessLauncherTests
{
    [Fact]
    public async Task WindowsWirelessDisplayLauncher_IsSupported_AndScanNearbyAsync_ReturnsDevices()
    {
        var launcher = new WindowsWirelessDisplayLauncher();
        Assert.True(launcher.IsSupported);

        var devices = await launcher.ScanNearbyAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(devices);
        Assert.Equal(3, devices.Count);
        Assert.Contains(devices, d => d.Name == "Living room TV" && d.DiagonalInches == 55.0);
        Assert.Contains(devices, d => d.Name == "Bedroom TV" && d.DiagonalInches == 43.0);
        Assert.Contains(devices, d => d.Name == "Office display" && d.DiagonalInches == 27.0);
    }

    [Fact]
    public async Task WindowsWirelessDisplayLauncher_OpenWirelessDisplayPickerAsync_InvokesCorrectSettingsUri()
    {
        string? launchedUri = null;
        var launcher = new WindowsWirelessDisplayLauncher(uri => launchedUri = uri);

        await launcher.OpenWirelessDisplayPickerAsync(TestContext.Current.CancellationToken);

        Assert.Equal("ms-settings-connectabledevices:devicediscovery", launchedUri);
    }

    [Fact]
    public async Task MacWirelessDisplayLauncher_IsSupported_AndScanNearbyAsync_ReturnsDevices()
    {
        var launcher = new MacWirelessDisplayLauncher();
        Assert.True(launcher.IsSupported);

        var devices = await launcher.ScanNearbyAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(devices);
        Assert.Equal(2, devices.Count);
        Assert.Contains(devices, d => d.Name == "Living Room Apple TV" && d.DiagonalInches == 65.0);
        Assert.Contains(devices, d => d.Name == "Office iPad" && d.DiagonalInches == 12.9);
    }

    [Fact]
    public async Task MacWirelessDisplayLauncher_OpenWirelessDisplayPickerAsync_InvokesOpenCommand()
    {
        string? launchedCommand = null;
        string? launchedArgs = null;
        var launcher = new MacWirelessDisplayLauncher((cmd, args) =>
        {
            launchedCommand = cmd;
            launchedArgs = args;
        });

        await launcher.OpenWirelessDisplayPickerAsync(TestContext.Current.CancellationToken);

        Assert.Equal("open", launchedCommand);
        Assert.Equal("x-apple.systempreferences:com.apple.Displays-Settings.extension", launchedArgs);
    }

    [Fact]
    public async Task LinuxWirelessDisplayLauncher_SupportedCheck_AndScanNearbyAsync()
    {
        var unsupportedLauncher = new LinuxWirelessDisplayLauncher(() => false);
        Assert.False(unsupportedLauncher.IsSupported);

        var supportedLauncher = new LinuxWirelessDisplayLauncher(() => true);
        Assert.True(supportedLauncher.IsSupported);

        var devices = await supportedLauncher.ScanNearbyAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(devices);
        Assert.Equal(2, devices.Count);
        Assert.Contains(devices, d => d.Name == "Conference Room Display" && d.DiagonalInches == 65.0);
        Assert.Contains(devices, d => d.Name == "Meeting Room TV" && d.DiagonalInches == 50.0);
    }

    [Fact]
    public async Task LinuxWirelessDisplayLauncher_OpenWirelessDisplayPickerAsync_WhenSupported_LaunchesGnomeNetworkDisplays()
    {
        string? launchedBinary = null;
        var launcher = new LinuxWirelessDisplayLauncher(() => true, bin => launchedBinary = bin);

        await launcher.OpenWirelessDisplayPickerAsync(TestContext.Current.CancellationToken);

        Assert.Equal("gnome-network-displays", launchedBinary);
    }

    [Fact]
    public async Task LinuxWirelessDisplayLauncher_OpenWirelessDisplayPickerAsync_WhenNotSupported_DoesNotLaunch()
    {
        string? launchedBinary = null;
        var launcher = new LinuxWirelessDisplayLauncher(() => false, bin => launchedBinary = bin);

        await launcher.OpenWirelessDisplayPickerAsync(TestContext.Current.CancellationToken);

        Assert.Null(launchedBinary);
    }
}

