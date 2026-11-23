namespace ZienDisplayFixer.Tests.Edid;

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZienDisplayFixer.App.Services;
using ZienDisplayFixer.App.Services.Linux;
using ZienDisplayFixer.App.Services.MacOS;
using ZienDisplayFixer.App.Services.Windows;
using ZienDisplayFixer.Core.Edid;
using ZienDisplayFixer.Core.Interfaces;

public class EdidReaderTests
{
    [Fact]
    public void AllEdidReaders_ImplementIEdidReader()
    {
        Assert.IsAssignableFrom<IEdidReader>(new WindowsEdidReader());
        Assert.IsAssignableFrom<IEdidReader>(new LinuxEdidReader());
        Assert.IsAssignableFrom<IEdidReader>(new MacEdidReader());
    }

    [Fact]
    public async Task LinuxEdidReader_OnNonLinuxPlatform_ReturnsGracefully()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return;
        }

        var reader = new LinuxEdidReader();
        var ct = TestContext.Current.CancellationToken;

        var all = await reader.ReadAllEdidsAsync(ct);
        Assert.NotNull(all);
        Assert.Empty(all);

        var single = await reader.ReadRawEdidAsync("eDP-1", ct);
        Assert.Null(single);
    }

    [Fact]
    public async Task MacEdidReader_OnNonMacPlatform_ReturnsGracefully()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        var reader = new MacEdidReader();
        var ct = TestContext.Current.CancellationToken;

        var all = await reader.ReadAllEdidsAsync(ct);
        Assert.NotNull(all);
        Assert.Empty(all);

        var single = await reader.ReadRawEdidAsync("1", ct);
        Assert.Null(single);
    }

    [Fact]
    public async Task WindowsEdidReader_OnWindowsPlatform_ReadsRegisteredEdids()
    {
        var ct = TestContext.Current.CancellationToken;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var nonWinReader = new WindowsEdidReader();
            var emptyAll = await nonWinReader.ReadAllEdidsAsync(ct);
            Assert.Empty(emptyAll);
            var emptySingle = await nonWinReader.ReadRawEdidAsync(@"\\.\DISPLAY1", ct);
            Assert.Null(emptySingle);
            return;
        }

        var reader = new WindowsEdidReader();

        var edids = await reader.ReadAllEdidsAsync(ct);
        Assert.NotNull(edids);

        // If host has at least one connected display with EDID in registry
        if (edids.Count > 0)
        {
            foreach (var kvp in edids)
            {
                Assert.NotNull(kvp.Key);
                Assert.NotNull(kvp.Value);
                Assert.True(kvp.Value.Length >= 128);

                var parsed = EdidParser.Parse(kvp.Value);
                Assert.NotNull(parsed);
                Assert.False(string.IsNullOrEmpty(parsed.ManufacturerId));
            }
        }
    }

    [Fact]
    public void ServiceRegistration_RegistersIEdidReaderOnSupportedPlatform()
    {
        var services = new ServiceCollection();
        services.AddDisplayFixerServices();
        var provider = services.BuildServiceProvider();

        var reader = provider.GetService<IEdidReader>();
        Assert.NotNull(reader);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.IsType<WindowsEdidReader>(reader);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Assert.IsType<MacEdidReader>(reader);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Assert.IsType<LinuxEdidReader>(reader);
        }
    }
}
