namespace ZienDisplayFixer.Tests.ViewModels;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Xunit;
using ZienDisplayFixer.App.Services;
using ZienDisplayFixer.App.ViewModels;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public class DisplaysViewModelTests
{
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

    private class FakeWallpaperService : IWallpaperService
    {
        public string? CurrentPath { get; set; } = "C:\\fake\\wallpaper.png";
        public byte[]? ImageBytes { get; set; } = ValidPngBytes;
        public event EventHandler? WallpaperChanged;

        public Task<string?> GetCurrentWallpaperPathAsync(CancellationToken ct = default)
        {
            return Task.FromResult(CurrentPath);
        }

        public Task<Stream?> GetWallpaperImageStreamAsync(CancellationToken ct = default)
        {
            if (ImageBytes == null) return Task.FromResult<Stream?>(null);
            return Task.FromResult<Stream?>(new MemoryStream(ImageBytes));
        }

        public void TriggerWallpaperChanged() => WallpaperChanged?.Invoke(this, EventArgs.Empty);
    }

    [Fact]
    public void DeviceType_SwitchLaptopAndPc_UpdatesIsLaptopAndSyncsState()
    {
        var state = new DisplaySetupState();
        var vm = new DisplaysViewModel(state);

        Assert.Equal(DeviceType.Laptop, vm.DeviceType);
        Assert.True(vm.IsLaptop);

        vm.SetDeviceTypeCommand.Execute("Pc");
        Assert.Equal(DeviceType.Pc, vm.DeviceType);
        Assert.False(vm.IsLaptop);
        Assert.Equal(DeviceType.Pc, state.DeviceType);

        vm.SetDeviceTypeCommand.Execute(DeviceType.Laptop);
        Assert.Equal(DeviceType.Laptop, vm.DeviceType);
        Assert.True(vm.IsLaptop);
        Assert.Equal(DeviceType.Laptop, state.DeviceType);
    }

    [Fact]
    public void SelectedLaptopScreenSize_Change_SyncsWithSetupState()
    {
        var state = new DisplaySetupState();
        var vm = new DisplaysViewModel(state);

        Assert.Equal(14.0, vm.SelectedLaptopScreenSize);
        Assert.Equal([13, 14, 15, 16, 17], vm.LaptopScreenSizes);

        vm.SelectedLaptopScreenSize = 16.0;
        Assert.Equal(16.0, state.LaptopScreenSize);
    }

    [Fact]
    public void SelectWallpaperPreset_DuskDuneMistGraphite_SetsBrushAndPreset()
    {
        var state = new DisplaySetupState();
        var vm = new DisplaysViewModel(state);

        vm.SelectWallpaperPresetCommand.Execute("Dusk");
        Assert.Equal("Dusk", vm.SelectedWallpaperPreset);
        Assert.Same(DisplaysViewModel.DuskBrush, vm.WallpaperBrush);
        Assert.Same(DisplaysViewModel.DuskBrush, state.WallpaperBrush);

        vm.SelectWallpaperPresetCommand.Execute("Dune");
        Assert.Equal("Dune", vm.SelectedWallpaperPreset);
        Assert.Same(DisplaysViewModel.DuneBrush, vm.WallpaperBrush);

        vm.SelectWallpaperPresetCommand.Execute("Mist");
        Assert.Equal("Mist", vm.SelectedWallpaperPreset);
        Assert.Same(DisplaysViewModel.MistBrush, vm.WallpaperBrush);

        vm.SelectWallpaperPresetCommand.Execute("Graphite");
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
    public void DemoMode_InitializesWithDefaultDisplays()
    {
        var state = new DisplaySetupState();
        var vm = new DisplaysViewModel(state);

        Assert.True(vm.IsDemoModeEnabled);
        Assert.Equal(2, vm.DisplayItems.Count);
        Assert.Equal(2, state.Displays.Count);

        var first = vm.DisplayItems[0];
        Assert.Equal(27.0, first.DiagonalInches);
        Assert.Equal("16:9", first.AspectRatio);
        Assert.Equal(ConnectionType.Hdmi, first.Connection);
    }

    [Fact]
    public void AddDisplayCommand_AddsDisplayUntilMax8()
    {
        var state = new DisplaySetupState();
        var vm = new DisplaysViewModel(state);

        Assert.Equal(2, vm.DisplayItems.Count);
        Assert.True(vm.CanAddDisplay);

        for (int i = 3; i <= 8; i++)
        {
            Assert.True(vm.CanAddDisplay);
            vm.AddDisplayCommand.Execute(null);
            Assert.Equal(i, vm.DisplayItems.Count);
            Assert.Equal(i, state.Displays.Count);
        }

        Assert.Equal(8, vm.DisplayItems.Count);
        Assert.False(vm.CanAddDisplay);

        // Attempting to add beyond 8 does not exceed limit
        vm.AddDisplayCommand.Execute(null);
        Assert.Equal(8, vm.DisplayItems.Count);
    }

    [Fact]
    public void RemoveDisplayCommand_RemovesDisplayAndReEnablesAdd()
    {
        var state = new DisplaySetupState();
        var vm = new DisplaysViewModel(state);

        while (vm.DisplayItems.Count < 8)
        {
            vm.AddDisplayCommand.Execute(null);
        }
        Assert.Equal(8, vm.DisplayItems.Count);
        Assert.False(vm.CanAddDisplay);

        var toRemove = vm.DisplayItems.Last();
        vm.RemoveDisplayCommand.Execute(toRemove);

        Assert.Equal(7, vm.DisplayItems.Count);
        Assert.True(vm.CanAddDisplay);
        Assert.Equal(7, state.Displays.Count);
    }

    [Fact]
    public void DisplayItem_ChangeSizeAndAspectRatio_RecalculatesPhysicalDimensionsAndSyncs()
    {
        var state = new DisplaySetupState();
        var vm = new DisplaysViewModel(state);

        var item = vm.DisplayItems[0];
        item.DiagonalInches = 34.0;
        item.AspectRatio = "21:9";
        item.Connection = ConnectionType.UsbC;

        var synced = state.Displays[0];
        Assert.Equal(ConnectionType.UsbC, synced.Connection);
        Assert.True(synced.PhysicalWidthMm > 700.0);
        Assert.True(synced.PhysicalHeightMm > 300.0);
    }

    [Fact]
    public void IsDemoModeEnabled_ControlsCanAddDisplay_AndDisplayIndicesAreSequential()
    {
        var state = new DisplaySetupState();
        var vm = new DisplaysViewModel(state);

        Assert.True(vm.IsDemoModeEnabled);
        Assert.True(vm.CanAddDisplay);

        // Indices with Laptop (offset +1 for external monitors)
        Assert.Equal(2, vm.DisplayItems[0].DisplayIndex);
        Assert.Equal("#2", vm.DisplayItems[0].DisplayNumberText);
        Assert.Equal(3, vm.DisplayItems[1].DisplayIndex);
        Assert.Equal("#3", vm.DisplayItems[1].DisplayNumberText);

        // When DemoMode is disabled, CanAddDisplay becomes false
        vm.IsDemoModeEnabled = false;
        Assert.False(vm.CanAddDisplay);

        vm.IsDemoModeEnabled = true;
        Assert.True(vm.CanAddDisplay);

        // Switch to PC (no laptop screen offset)
        vm.SetDeviceTypeCommand.Execute("Pc");
        Assert.Equal(1, vm.DisplayItems[0].DisplayIndex);
        Assert.Equal("#1", vm.DisplayItems[0].DisplayNumberText);
        Assert.Equal(2, vm.DisplayItems[1].DisplayIndex);
        Assert.Equal("#2", vm.DisplayItems[1].DisplayNumberText);
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
        Assert.Equal(displaysVm.DisplayItems.Count, homeVm.Displays.Count);

        // Change on DisplaysViewModel
        displaysVm.SetDeviceTypeCommand.Execute("Pc");
        Assert.Equal(DeviceType.Pc, homeVm.DeviceType);

        displaysVm.SelectWallpaperPresetCommand.Execute("Dusk");
        Assert.Equal(DisplaysViewModel.DuskBrush, homeVm.WallpaperBrush);

        displaysVm.AddDisplayCommand.Execute(null);
        Assert.Equal(displaysVm.DisplayItems.Count, homeVm.Displays.Count);

        // Change on HomeViewModel
        homeVm.DeviceType = DeviceType.Laptop;
        Assert.Equal(DeviceType.Laptop, displaysVm.DeviceType);
    }
}

