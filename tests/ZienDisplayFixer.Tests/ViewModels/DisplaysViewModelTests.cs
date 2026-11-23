namespace ZienDisplayFixer.Tests.ViewModels;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZienDisplayFixer.App.Services;
using ZienDisplayFixer.App.ViewModels;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public class DisplaysViewModelTests
{
    private class FakeWallpaperService : IWallpaperService
    {
        public event EventHandler? WallpaperChanged { add { } remove { } }

        public Task<string?> GetCurrentWallpaperPathAsync(CancellationToken ct = default)
        {
            var temp = Path.GetTempFileName();
            File.WriteAllBytes(temp, ValidPngBytes);
            return Task.FromResult<string?>(temp);
        }

        public Task<Stream?> GetWallpaperImageStreamAsync(CancellationToken ct = default)
        {
            Stream stream = new MemoryStream(ValidPngBytes);
            return Task.FromResult<Stream?>(stream);
        }
    }

    private static readonly byte[] ValidPngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    ];

    [Fact]
    public void InitialState_DefaultsToLaptopAndGraphite()
    {
        var state = new DisplaySetupState();
        var vm = new DisplaysViewModel(state);

        Assert.Equal(DeviceType.Laptop, vm.DeviceType);
        Assert.True(vm.IsLaptop);
        Assert.Equal(14.0, vm.SelectedLaptopScreenSize);
        Assert.Equal("Graphite", vm.SelectedWallpaperPreset);
        Assert.NotNull(vm.WallpaperBrush);
    }

    [Fact]
    public void SetDeviceTypeCommand_UpdatesDeviceTypeAndSyncs()
    {
        var state = new DisplaySetupState();
        var vm = new DisplaysViewModel(state);

        vm.SetDeviceTypeCommand.Execute("Pc");
        Assert.Equal(DeviceType.Pc, vm.DeviceType);
        Assert.False(vm.IsLaptop);
        Assert.Equal(DeviceType.Pc, state.DeviceType);

        vm.SetDeviceTypeCommand.Execute("Laptop");
        Assert.Equal(DeviceType.Laptop, vm.DeviceType);
        Assert.True(vm.IsLaptop);
        Assert.Equal(DeviceType.Laptop, state.DeviceType);
    }

    [Fact]
    public void SetWallpaperPresetCommand_UpdatesBrushAndPreset()
    {
        var state = new DisplaySetupState();
        var vm = new DisplaysViewModel(state);

        vm.SetWallpaperPresetCommand.Execute("Dusk");
        Assert.Equal("Dusk", vm.SelectedWallpaperPreset);
        Assert.Same(DisplaysViewModel.DuskBrush, vm.WallpaperBrush);

        vm.SetWallpaperPresetCommand.Execute("Dune");
        Assert.Equal("Dune", vm.SelectedWallpaperPreset);
        Assert.Same(DisplaysViewModel.DuneBrush, vm.WallpaperBrush);

        vm.SetWallpaperPresetCommand.Execute("Mist");
        Assert.Equal("Mist", vm.SelectedWallpaperPreset);
        Assert.Same(DisplaysViewModel.MistBrush, vm.WallpaperBrush);

        vm.SetWallpaperPresetCommand.Execute("Graphite");
        Assert.Equal("Graphite", vm.SelectedWallpaperPreset);
        Assert.Same(DisplaysViewModel.GraphiteBrush, vm.WallpaperBrush);
    }

    [Fact]
    public async Task UseSystemWallpaperCommand_WhenServiceReturnsStream_CreatesImageBrushAndSetsPreset()
    {
        var state = new DisplaySetupState();
        var wallpaperService = new FakeWallpaperService();
        var vm = new DisplaysViewModel(state, wallpaperService);

        await vm.UseSystemWallpaperAsync(TestContext.Current.CancellationToken);

        Assert.Equal("System", vm.SelectedWallpaperPreset);
        Assert.NotNull(vm.WallpaperBrush);
        Assert.NotNull(state.WallpaperBrush);
    }

    [Fact]
    public void SetCustomWallpaper_WithStream_SetsImageBrushAndUploadPreset()
    {
        var state = new DisplaySetupState();
        var vm = new DisplaysViewModel(state);

        using var stream = new MemoryStream(ValidPngBytes);
        vm.SetCustomWallpaper(stream);

        Assert.Equal("Upload", vm.SelectedWallpaperPreset);
        Assert.NotNull(vm.WallpaperBrush);
    }

    [Fact]
    public void Displays_WhenPopulatedInSetupState_SyncsToDisplayItems()
    {
        var state = new DisplaySetupState();
        state.Displays.Add(new DisplayInfo(
            id: "d1",
            name: "LG UltraFine 27",
            model: "LG UltraFine 27",
            isPrimary: true,
            positionX: 0,
            positionY: 0,
            currentMode: new DisplayMode(3840, 2160, 60, false),
            physicalWidthMm: 597,
            physicalHeightMm: 336,
            connection: ConnectionType.UsbC));

        var vm = new DisplaysViewModel(state);

        Assert.True(vm.HasDisplays);
        Assert.Single(vm.DisplayItems);
        var item = vm.DisplayItems[0];
        Assert.Equal("LG UltraFine 27", item.Name);
        Assert.Equal(27.0, item.DiagonalInches);
        Assert.Equal("16:9", item.AspectRatio);
        Assert.Equal(ConnectionType.UsbC, item.Connection);
        Assert.Equal("USB-C", item.ConnectionText);
        Assert.Equal("3840 × 2160 @ 60Hz", item.CurrentModeText);
    }

    [Fact]
    public void StateSynchronization_HomeViewModelAndDisplaysViewModel_KeepInSync()
    {
        var state = new DisplaySetupState();
        var displaysVm = new DisplaysViewModel(state);
        var homeVm = new HomeViewModel(null, null, null, state);

        // Verify initial sync
        Assert.Equal(displaysVm.DeviceType, homeVm.DeviceType);
        Assert.Equal(displaysVm.SelectedLaptopScreenSize, homeVm.LaptopScreenSize);
        Assert.Equal(displaysVm.WallpaperBrush, homeVm.WallpaperBrush);

        // Change on DisplaysViewModel
        displaysVm.SetDeviceTypeCommand.Execute("Pc");
        Assert.Equal(DeviceType.Pc, homeVm.DeviceType);

        displaysVm.SetWallpaperPresetCommand.Execute("Dusk");
        Assert.Equal(DisplaysViewModel.DuskBrush, homeVm.WallpaperBrush);

        // Change on HomeViewModel
        homeVm.DeviceType = DeviceType.Laptop;
        Assert.Equal(DeviceType.Laptop, displaysVm.DeviceType);
    }
}
