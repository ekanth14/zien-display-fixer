namespace ZienDisplayFixer.Tests.Services;

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZienDisplayFixer.App.Services.MacOS;
using ZienDisplayFixer.App.Services.MacOS.Native;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public class MacDisplayServiceTests
{
    [Fact]
    public void MacDisplayService_ImplementsIDisplayServiceAndIDisposable()
    {
        using var service = new MacDisplayService();
        Assert.IsAssignableFrom<IDisplayService>(service);
        Assert.IsAssignableFrom<IDisposable>(service);
    }

    [Fact]
    public async Task MacDisplayService_OnNonMacPlatform_ReturnsEmptyOrFalseGracefully()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return; // Skip non-Mac assertions when running natively on macOS
        }

        using var service = new MacDisplayService();

        var ct = TestContext.Current.CancellationToken;

        var displays = await service.GetDisplaysAsync(ct);
        Assert.NotNull(displays);
        Assert.Empty(displays);

        var modes = await service.GetModesAsync("1", ct);
        Assert.NotNull(modes);
        Assert.Empty(modes);

        var testResult = await service.TestModeAsync("1", new DisplayMode(1920, 1080, 60.0), ct);
        Assert.False(testResult);

        var applyResult = await service.ApplyModeAsync("1", new DisplayMode(1920, 1080, 60.0), ct);
        Assert.False(applyResult);

        var restoreResult = await service.RestorePreviousModesAsync(ct);
        Assert.False(restoreResult);
    }

    [Fact]
    public void MacDisplayService_Lifecycle_MultipleDisposeCalls_DoNotThrow()
    {
        var service = new MacDisplayService();
        service.Dispose();
        service.Dispose(); // Idempotent dispose
    }

    [Fact]
    public void MacDisplayService_DisplaysChanged_CanSubscribeAndUnsubscribe()
    {
        using var service = new MacDisplayService();
        EventHandler handler = (sender, args) => { };

        service.DisplaysChanged += handler;
        service.DisplaysChanged -= handler;
    }

    [Fact]
    public void CoreGraphics_StructLayouts_MatchApple64BitSizes()
    {
        Assert.Equal(16, Marshal.SizeOf<CoreGraphics.CGPoint>());
        Assert.Equal(16, Marshal.SizeOf<CoreGraphics.CGSize>());
        Assert.Equal(32, Marshal.SizeOf<CoreGraphics.CGRect>());
    }

    [Fact]
    public void CoreGraphics_Constants_MatchExpectations()
    {
        Assert.Equal(0u, CoreGraphics.kCGConfigureForCommandLine);
        Assert.Equal(1u, CoreGraphics.kCGConfigurePermanently);
        Assert.Equal(2u, CoreGraphics.kCGConfigureForSession);
        Assert.Equal(1u, CoreGraphics.kCGDisplayBeginConfigurationFlag);
        Assert.Equal(0x00000040u, CoreGraphics.kDisplayModeInterlacedFlag);
        Assert.Equal(0, CoreGraphics.kCGErrorSuccess);
    }

    [Fact]
    public void ServiceRegistration_MacDisplayService_CanBeResolvedFromContainer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDisplayService, MacDisplayService>();
        var provider = services.BuildServiceProvider();

        var resolved = provider.GetService<IDisplayService>();
        Assert.NotNull(resolved);
        Assert.IsType<MacDisplayService>(resolved);
    }
}
