namespace ZienDisplayFixer.Tests.Services;

using System;
using System.IO;
using Microsoft.Win32;
using Xunit;
using ZienDisplayFixer.App.Services.Linux;
using ZienDisplayFixer.App.Services.MacOS;
using ZienDisplayFixer.App.Services.Windows;
using ZienDisplayFixer.App.ViewModels;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public class AutostartServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _testRegSubKey;

    public AutostartServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ZienAutostartTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        if (OperatingSystem.IsWindows())
        {
            _testRegSubKey = @"Software\ZienAutostartTests_" + Guid.NewGuid().ToString("N");
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Best effort
        }

        if (OperatingSystem.IsWindows() && _testRegSubKey != null)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(_testRegSubKey, false);
            }
            catch
            {
                // Best effort
            }
        }
    }

    [Fact]
    public void MacAutostartService_EnableAndDisable_WritesAndRemovesPlistFile()
    {
        var plistPath = Path.Combine(_tempDir, "LaunchAgents", "com.zien.displayfixer.plist");
        var service = new MacAutostartService(plistPath);

        Assert.False(service.IsAutostartEnabled());

        service.SetAutostart(true);
        Assert.True(service.IsAutostartEnabled());
        Assert.True(File.Exists(plistPath));

        var content = File.ReadAllText(plistPath);
        Assert.Contains("com.zien.displayfixer", content);
        Assert.Contains("--minimized", content);
        Assert.Contains("<key>RunAtLoad</key>", content);

        service.SetAutostart(false);
        Assert.False(service.IsAutostartEnabled());
        Assert.False(File.Exists(plistPath));
    }

    [Fact]
    public void LinuxAutostartService_EnableAndDisable_WritesAndRemovesDesktopFile()
    {
        var desktopPath = Path.Combine(_tempDir, "autostart", "zien-display-fixer.desktop");
        var service = new LinuxAutostartService(desktopPath);

        Assert.False(service.IsAutostartEnabled());

        service.SetAutostart(true);
        Assert.True(service.IsAutostartEnabled());
        Assert.True(File.Exists(desktopPath));

        var content = File.ReadAllText(desktopPath);
        Assert.Contains("[Desktop Entry]", content);
        Assert.Contains("--minimized", content);
        Assert.Contains("X-GNOME-Autostart-enabled=true", content);

        service.SetAutostart(false);
        Assert.False(service.IsAutostartEnabled());
        Assert.False(File.Exists(desktopPath));
    }

    [Fact]
    public void WindowsAutostartService_EnableAndDisable_WritesAndRemovesRegistryValue()
    {
        if (!OperatingSystem.IsWindows())
        {
            var nonWinService = new WindowsAutostartService();
            Assert.False(nonWinService.IsAutostartEnabled());
            nonWinService.SetAutostart(true);
            Assert.False(nonWinService.IsAutostartEnabled());
            return;
        }

        var service = new WindowsAutostartService(_testRegSubKey!);

        Assert.False(service.IsAutostartEnabled());

        service.SetAutostart(true);
        Assert.True(service.IsAutostartEnabled());

        using (var key = Registry.CurrentUser.OpenSubKey(_testRegSubKey!, false))
        {
            var value = key?.GetValue("ZienDisplayFixer") as string;
            Assert.NotNull(value);
            Assert.Contains("--minimized", value);
        }

        service.SetAutostart(false);
        Assert.False(service.IsAutostartEnabled());

        using (var key = Registry.CurrentUser.OpenSubKey(_testRegSubKey!, false))
        {
            var value = key?.GetValue("ZienDisplayFixer");
            Assert.Null(value);
        }
    }

    private class FakeAutostartService : IAutostartService
    {
        public bool Enabled { get; set; }
        public int SetAutostartCallCount { get; private set; }

        public bool IsAutostartEnabled() => Enabled;

        public void SetAutostart(bool enable)
        {
            SetAutostartCallCount++;
            Enabled = enable;
        }
    }

    [Fact]
    public void SettingsViewModel_WithAutostartService_InitializesAndPropagatesChanges()
    {
        var autostart = new FakeAutostartService { Enabled = true };
        var vm = new SettingsViewModel(null, null, autostart);

        Assert.True(vm.StartAtLogin);

        vm.StartAtLogin = false;
        Assert.False(autostart.Enabled);
        Assert.Equal(1, autostart.SetAutostartCallCount);

        vm.StartAtLogin = true;
        Assert.True(autostart.Enabled);
        Assert.Equal(2, autostart.SetAutostartCallCount);
    }
}

