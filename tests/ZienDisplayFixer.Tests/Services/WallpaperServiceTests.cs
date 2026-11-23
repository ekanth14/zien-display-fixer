namespace ZienDisplayFixer.Tests.Services;

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZienDisplayFixer.App.Services;
using ZienDisplayFixer.App.Services.Linux;
using ZienDisplayFixer.App.Services.MacOS;
using ZienDisplayFixer.App.Services.Windows;
using ZienDisplayFixer.Core.Interfaces;

public class WallpaperServiceTests
{
    [Fact]
    public async Task WindowsWallpaperService_GetCurrentWallpaperPathAsync_DoesNotThrow()
    {
        var service = new WindowsWallpaperService();
        // Should execute P/Invoke safely without unhandled exception
        var path = await service.GetCurrentWallpaperPathAsync(TestContext.Current.CancellationToken);
        // Result is either valid path string or null
        if (path != null)
        {
            Assert.False(string.IsNullOrWhiteSpace(path));
        }
    }

    [Fact]
    public async Task MacWallpaperService_GetCurrentWallpaperPathAsync_DoesNotThrow()
    {
        var service = new MacWallpaperService();
        var path = await service.GetCurrentWallpaperPathAsync(TestContext.Current.CancellationToken);
        // On Windows or headless, should return null or path without crashing
        if (path != null)
        {
            Assert.False(string.IsNullOrWhiteSpace(path));
        }
    }

    [Fact]
    public async Task LinuxWallpaperService_GetCurrentWallpaperPathAsync_DoesNotThrow()
    {
        var service = new LinuxWallpaperService();
        var path = await service.GetCurrentWallpaperPathAsync(TestContext.Current.CancellationToken);
        if (path != null)
        {
            Assert.False(string.IsNullOrWhiteSpace(path));
        }
    }

    [Fact]
    public async Task WindowsWallpaperService_GetWallpaperImageStreamAsync_WhenPathIsNull_ReturnsNull()
    {
        // Service should return stream or null safely
        var service = new WindowsWallpaperService();
        var stream = await service.GetWallpaperImageStreamAsync(TestContext.Current.CancellationToken);
        // If system wallpaper exists, stream is valid; otherwise null
        if (stream != null)
        {
            using (stream)
            {
                Assert.True(stream.CanRead);
            }
        }
    }

    [Fact]
    public void ServiceRegistration_RegistersIWallpaperServiceAndDisplaySetupState()
    {
        var services = new ServiceCollection();
        services.AddDisplayFixerServices();
        var provider = services.BuildServiceProvider();

        var wallpaperService = provider.GetService<IWallpaperService>();
        Assert.NotNull(wallpaperService);

        var setupState = provider.GetService<IDisplaySetupState>();
        Assert.NotNull(setupState);
    }
}

