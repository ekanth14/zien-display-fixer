namespace ZienDisplayFixer.App.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZienDisplayFixer.App.Services;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public partial class DisplaysViewModel : ViewModelBase
{
    private readonly IDisplaySetupState? _setupState;
    private readonly IWallpaperService? _wallpaperService;
    private readonly IDisplayService? _displayService;
    private bool _isSyncing;

    public string Title => "Displays";

    public static readonly LinearGradientBrush DuskBrush = CreateGradient("#6A5A8E", "#1B1630");
    public static readonly LinearGradientBrush DuneBrush = CreateGradient("#B9966B", "#2E2218");
    public static readonly LinearGradientBrush MistBrush = CreateGradient("#8FA3AD", "#1F2830");
    public static readonly LinearGradientBrush GraphiteBrush = CreateGradient("#3A3F4C", "#0D0F13");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLaptop))]
    private DeviceType _deviceType = DeviceType.Laptop;

    public bool IsLaptop => DeviceType == DeviceType.Laptop;

    public IReadOnlyList<double> LaptopScreenSizes { get; } = [13, 14, 15, 16, 17];

    [ObservableProperty]
    private double _selectedLaptopScreenSize = 14.0;

    [ObservableProperty]
    private IBrush? _wallpaperBrush = GraphiteBrush;

    [ObservableProperty]
    private string _selectedWallpaperPreset = "Graphite";

    public string WallpaperHintText => "The app will use your current Windows/system wallpaper.";

    [ObservableProperty]
    private bool _isDemoModeEnabled = true;

    private IDisposable? _currentImageBitmap;

    public ObservableCollection<DisplayItemViewModel> DisplayItems { get; } = [];

    public bool CanAddDisplay => IsDemoModeEnabled && DisplayItems.Count < 8;

    public static readonly IReadOnlyList<string> DefaultAspectRatioOptions =
    [
        "16:9",
        "16:10",
        "21:9",
        "32:9",
        "4:3",
        "5:4"
    ];

    public static readonly IReadOnlyList<ConnectionType> DefaultConnectionOptions =
    [
        ConnectionType.Hdmi,
        ConnectionType.DisplayPort,
        ConnectionType.UsbC,
        ConnectionType.Usb4,
        ConnectionType.Vga,
        ConnectionType.WiFi
    ];

    public IReadOnlyList<string> AspectRatioOptions => DefaultAspectRatioOptions;

    public IReadOnlyList<ConnectionType> ConnectionOptions => DefaultConnectionOptions;

    public DisplaysViewModel()
        : this(null, null, null)
    {
    }

    public DisplaysViewModel(
        IDisplaySetupState? setupState,
        IWallpaperService? wallpaperService = null,
        IDisplayService? displayService = null)
    {
        _setupState = setupState;
        _wallpaperService = wallpaperService;
        _displayService = displayService;

        if (_setupState != null)
        {
            _deviceType = _setupState.DeviceType;
            _selectedLaptopScreenSize = _setupState.LaptopScreenSize;
            _wallpaperBrush = _setupState.WallpaperBrush ?? GraphiteBrush;
            _selectedWallpaperPreset = _setupState.SelectedWallpaperPreset;
            _isDemoModeEnabled = _setupState.IsDemoModeEnabled;

            _setupState.SetupChanged += OnSetupStateChanged;
        }

        // Initialize displays
        if (_isDemoModeEnabled)
        {
            InitializeDemoDisplays();
        }
        else
        {
            _ = LoadDisplaysFromServiceAsync();
        }
    }

    private void InitializeDemoDisplays()
    {
        foreach (var item in DisplayItems)
        {
            item.Changed -= OnItemChanged;
        }

        if (_setupState != null && _setupState.Displays.Count > 0)
        {
            DisplayItems.Clear();
            foreach (var d in _setupState.Displays)
            {
                var item = DisplayItemViewModel.FromDisplayInfo(d, RemoveDisplay);
                item.Changed += OnItemChanged;
                DisplayItems.Add(item);
            }
        }
        else
        {
            DisplayItems.Clear();

            var d1 = new DisplayItemViewModel
            {
                Name = "Dell UltraSharp 27\"",
                DiagonalInches = 27.0,
                AspectRatio = "16:9",
                Connection = ConnectionType.Hdmi,
                NativeModeText = "2560 × 1440 @ 144Hz",
                CurrentMode = new DisplayMode(2560, 1440, 144.0, false)
            };
            d1.RemoveCommand = new RelayCommand(() => RemoveDisplay(d1));
            d1.Changed += OnItemChanged;
            DisplayItems.Add(d1);

            var d2 = new DisplayItemViewModel
            {
                Name = "Dell 24\" Monitor",
                DiagonalInches = 24.0,
                AspectRatio = "16:9",
                Connection = ConnectionType.DisplayPort,
                NativeModeText = "1920 × 1080 @ 60Hz",
                CurrentMode = new DisplayMode(1920, 1080, 60.0, false)
            };
            d2.RemoveCommand = new RelayCommand(() => RemoveDisplay(d2));
            d2.Changed += OnItemChanged;
            DisplayItems.Add(d2);

            SyncToSetupState();
        }

        UpdateDisplayIndices();
        AddDisplayCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanAddDisplay));
    }

    private async Task LoadDisplaysFromServiceAsync(CancellationToken ct = default)
    {
        if (_displayService == null) return;

        try
        {
            var raw = await _displayService.GetDisplaysAsync(ct).ConfigureAwait(false);
            foreach (var item in DisplayItems)
            {
                item.Changed -= OnItemChanged;
            }
            DisplayItems.Clear();
            foreach (var d in raw)
            {
                var item = DisplayItemViewModel.FromDisplayInfo(d, RemoveDisplay);
                item.Changed += OnItemChanged;
                DisplayItems.Add(item);
            }
            UpdateDisplayIndices();
            SyncToSetupState();
            AddDisplayCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CanAddDisplay));
        }
        catch
        {
            // Fallback to demo displays if service fails
            InitializeDemoDisplays();
        }
    }

    [RelayCommand]
    public void SetDeviceType(object? param)
    {
        if (param is DeviceType dt)
        {
            DeviceType = dt;
        }
        else if (param is string s)
        {
            if (string.Equals(s, "Laptop", StringComparison.OrdinalIgnoreCase))
                DeviceType = DeviceType.Laptop;
            else if (string.Equals(s, "Pc", StringComparison.OrdinalIgnoreCase))
                DeviceType = DeviceType.Pc;
        }
    }

    partial void OnDeviceTypeChanged(DeviceType value)
    {
        if (_setupState != null && _setupState.DeviceType != value)
        {
            _setupState.DeviceType = value;
        }
        UpdateDisplayIndices();
    }

    partial void OnSelectedLaptopScreenSizeChanged(double value)
    {
        if (_setupState != null && Math.Abs(_setupState.LaptopScreenSize - value) > 0.001)
        {
            _setupState.LaptopScreenSize = value;
        }
    }

    partial void OnWallpaperBrushChanged(IBrush? value)
    {
        if (_setupState != null && _setupState.WallpaperBrush != value)
        {
            _setupState.WallpaperBrush = value;
        }
    }

    partial void OnSelectedWallpaperPresetChanged(string value)
    {
        if (_setupState != null && _setupState.SelectedWallpaperPreset != value)
        {
            _setupState.SelectedWallpaperPreset = value;
        }
    }

    partial void OnIsDemoModeEnabledChanged(bool value)
    {
        if (_setupState != null && _setupState.IsDemoModeEnabled != value)
        {
            _setupState.IsDemoModeEnabled = value;
        }

        if (value)
        {
            InitializeDemoDisplays();
        }
        else
        {
            _ = LoadDisplaysFromServiceAsync();
        }

        AddDisplayCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanAddDisplay));
    }

    [RelayCommand]
    public void SelectWallpaperPreset(string presetName)
    {
        _currentImageBitmap?.Dispose();
        _currentImageBitmap = null;

        switch (presetName?.Trim().ToLowerInvariant())
        {
            case "dusk":
                WallpaperBrush = DuskBrush;
                SelectedWallpaperPreset = "Dusk";
                break;
            case "dune":
                WallpaperBrush = DuneBrush;
                SelectedWallpaperPreset = "Dune";
                break;
            case "mist":
                WallpaperBrush = MistBrush;
                SelectedWallpaperPreset = "Mist";
                break;
            case "graphite":
            default:
                WallpaperBrush = GraphiteBrush;
                SelectedWallpaperPreset = "Graphite";
                break;
        }
    }

    [RelayCommand]
    public async Task UseSystemWallpaperAsync(CancellationToken ct = default)
    {
        if (_wallpaperService == null) return;

        try
        {
            var stream = await _wallpaperService.GetWallpaperImageStreamAsync(ct).ConfigureAwait(false);
            if (stream != null)
            {
                using (stream)
                {
                    try
                    {
                        var bitmap = new Bitmap(stream);
                        _currentImageBitmap?.Dispose();
                        _currentImageBitmap = bitmap;
                        WallpaperBrush = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                    }
                    catch
                    {
                        // Fallback in headless environment where render interface is not active
                        _currentImageBitmap?.Dispose();
                        _currentImageBitmap = null;
                        WallpaperBrush = new SolidColorBrush(Color.Parse("#1A1A22"));
                    }
                    SelectedWallpaperPreset = "System";
                }
            }
        }
        catch
        {
            // Ignore wallpaper read errors
        }
    }

    [RelayCommand]
    public async Task UploadWallpaperAsync()
    {
        try
        {
            TopLevel? topLevel = null;
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                topLevel = desktop.MainWindow;
            }

            if (topLevel?.StorageProvider is { } sp)
            {
                var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Wallpaper Image",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("Images")
                        {
                            Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"]
                        }
                    ]
                });

                if (files.Count > 0)
                {
                    await using var stream = await files[0].OpenReadAsync();
                    SetCustomWallpaper(stream);
                }
            }
        }
        catch
        {
            // Ignore upload picker errors
        }
    }

    public void SetCustomWallpaper(Stream stream)
    {
        try
        {
            try
            {
                var bitmap = new Bitmap(stream);
                _currentImageBitmap?.Dispose();
                _currentImageBitmap = bitmap;
                WallpaperBrush = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
            }
            catch
            {
                // Fallback in headless environment
                _currentImageBitmap?.Dispose();
                _currentImageBitmap = null;
                WallpaperBrush = new SolidColorBrush(Color.Parse("#252830"));
            }
            SelectedWallpaperPreset = "Upload";
        }
        catch
        {
            // Ignore image parse errors
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddDisplay))]
    public void AddDisplay()
    {
        if (DisplayItems.Count >= 8) return;

        int num = DisplayItems.Count + 1;
        var item = new DisplayItemViewModel
        {
            Name = $"Display {num}",
            DiagonalInches = 27.0,
            AspectRatio = "16:9",
            Connection = ConnectionType.Hdmi,
            NativeModeText = "2560 × 1440 @ 144Hz",
            CurrentMode = new DisplayMode(2560, 1440, 144.0, false)
        };
        item.RemoveCommand = new RelayCommand(() => RemoveDisplay(item));
        item.Changed += OnItemChanged;

        DisplayItems.Add(item);
        UpdateDisplayIndices();
        AddDisplayCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanAddDisplay));
        SyncToSetupState();
    }

    [RelayCommand]
    public void RemoveDisplay(DisplayItemViewModel? item)
    {
        if (item == null) return;

        item.Changed -= OnItemChanged;
        DisplayItems.Remove(item);
        UpdateDisplayIndices();
        AddDisplayCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanAddDisplay));
        SyncToSetupState();
    }

    private void UpdateDisplayIndices()
    {
        int offset = DeviceType == DeviceType.Laptop ? 1 : 0;
        for (int i = 0; i < DisplayItems.Count; i++)
        {
            DisplayItems[i].DisplayIndex = i + 1 + offset;
        }
    }

    private void OnItemChanged(object? sender, EventArgs e)
    {
        SyncToSetupState();
    }

    private void SyncToSetupState()
    {
        if (_setupState == null || _isSyncing) return;

        try
        {
            _isSyncing = true;
            _setupState.Displays.Clear();
            foreach (var item in DisplayItems)
            {
                _setupState.Displays.Add(item.ToDisplayInfo());
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void OnSetupStateChanged(object? sender, EventArgs e)
    {
        if (_setupState == null || _isSyncing) return;

        DeviceType = _setupState.DeviceType;
        SelectedLaptopScreenSize = _setupState.LaptopScreenSize;
        WallpaperBrush = _setupState.WallpaperBrush ?? GraphiteBrush;
        SelectedWallpaperPreset = _setupState.SelectedWallpaperPreset;
        IsDemoModeEnabled = _setupState.IsDemoModeEnabled;
    }

    private static LinearGradientBrush CreateGradient(string startHex, string endHex)
    {
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.Parse(startHex), 0.0),
                new GradientStop(Color.Parse(endHex), 1.0)
            ]
        };
    }
}
