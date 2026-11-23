namespace ZienDisplayFixer.App.Services;

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ZienDisplayFixer.App.ViewModels;
using ZienDisplayFixer.Core.Models;

public partial class DisplaySetupState : ObservableObject, IDisplaySetupState
{
    [ObservableProperty]
    private DeviceType _deviceType = DeviceType.Laptop;

    [ObservableProperty]
    private double _laptopScreenSize = 14.0;

    [ObservableProperty]
    private IBrush? _wallpaperBrush = DisplaysViewModel.GraphiteBrush;

    [ObservableProperty]
    private string _selectedWallpaperPreset = "Graphite";

    [ObservableProperty]
    private bool _isDemoModeEnabled = true;

    public ObservableCollection<DisplayInfo> Displays { get; } = [];

    public event EventHandler? SetupChanged;

    public DisplaySetupState()
    {
        Displays.CollectionChanged += OnDisplaysCollectionChanged;
    }

    private void OnDisplaysCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SetupChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnDeviceTypeChanged(DeviceType value)
    {
        SetupChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnLaptopScreenSizeChanged(double value)
    {
        SetupChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnWallpaperBrushChanged(IBrush? value)
    {
        SetupChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSelectedWallpaperPresetChanged(string value)
    {
        SetupChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsDemoModeEnabledChanged(bool value)
    {
        SetupChanged?.Invoke(this, EventArgs.Empty);
    }
}

