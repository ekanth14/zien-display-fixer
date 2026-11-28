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
using ZienDisplayFixer.Core.Edid;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public partial class DisplaysViewModel : ViewModelBase
{
    private readonly IDisplaySetupState? _setupState;
    private readonly IWallpaperService? _wallpaperService;
    private readonly IDisplayService? _displayService;
    private readonly IEdidReader? _edidReader;
    private readonly ISettingsService? _settingsService;
    private bool _isSyncing;

    public string Title => "Displays";

    public static readonly IBrush DuskBrush = CreateGradient("#6A5A8E", "#1B1630");
    public static readonly IBrush DuneBrush = CreateGradient("#B9966B", "#2E2218");
    public static readonly IBrush MistBrush = CreateGradient("#8FA3AD", "#1F2830");
    public static readonly IBrush GraphiteBrush = CreateGradient("#3A3F4C", "#0D0F13");

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

    public string WallpaperHintText => "The app uses your current Windows/system wallpaper.";

    private IDisposable? _currentImageBitmap;

    public ObservableCollection<DisplayItemViewModel> DisplayItems { get; } = [];

    public bool HasDisplays => DisplayItems.Count > 0;

    public DisplaysViewModel()
        : this(null, null, null, null)
    {
    }

    public DisplaysViewModel(
        IDisplaySetupState? setupState,
        IWallpaperService? wallpaperService = null,
        IDisplayService? displayService = null,
        IEdidReader? edidReader = null,
        ISettingsService? settingsService = null)
    {
        _setupState = setupState;
        _wallpaperService = wallpaperService;
        _displayService = displayService;
        _edidReader = edidReader;
        _settingsService = settingsService;

        if (_setupState != null)
        {
            _deviceType = _setupState.DeviceType;
            _selectedLaptopScreenSize = _setupState.LaptopScreenSize;
            _wallpaperBrush = _setupState.WallpaperBrush ?? GraphiteBrush;
            _selectedWallpaperPreset = _setupState.SelectedWallpaperPreset;

            _setupState.SetupChanged += OnSetupStateChanged;
        }

        _ = LoadDisplaysFromServiceAsync();
    }

    public async Task LoadDisplaysFromServiceAsync(CancellationToken ct = default)
    {
        if (_setupState != null && _setupState.Displays.Count > 0)
        {
            SyncFromSetupStateDisplays();
            return;
        }

        if (_displayService == null)
        {
            DisplayItems.Clear();
            OnPropertyChanged(nameof(HasDisplays));
            return;
        }

        try
        {
            var raw = await _displayService.GetDisplaysAsync(ct).ConfigureAwait(false);
            var enriched = new List<DisplayInfo>();

            foreach (var d in raw)
            {
                var cur = d;
                if (_edidReader != null && cur.PhysicalWidthMm <= 0)
                {
                    try
                    {
                        var edidBytes = await _edidReader.ReadRawEdidAsync(cur.Id, ct).ConfigureAwait(false);
                        if (edidBytes != null)
                        {
                            var parsed = EdidParser.Parse(edidBytes);
                            if (parsed != null)
                            {
                                double wMm = parsed.PhysicalWidthCm > 0 ? parsed.PhysicalWidthCm * 10 : cur.PhysicalWidthMm;
                                double hMm = parsed.PhysicalHeightCm > 0 ? parsed.PhysicalHeightCm * 10 : cur.PhysicalHeightMm;
                                string model = !string.IsNullOrWhiteSpace(parsed.ModelName) ? parsed.ModelName : cur.Model;
                                string name = !string.IsNullOrWhiteSpace(parsed.ModelName) ? parsed.ModelName : cur.Name;

                                cur = cur with
                                {
                                    PhysicalWidthMm = wMm,
                                    PhysicalHeightMm = hMm,
                                    Model = model,
                                    Name = name,
                                    NativeMode = cur.NativeMode ?? parsed.PreferredMode
                                };
                            }
                        }
                    }
                    catch
                    {
                        // Keep raw display
                    }
                }
                enriched.Add(cur);
            }

            if (_setupState != null)
            {
                _setupState.Displays.Clear();
                foreach (var d in enriched)
                {
                    _setupState.Displays.Add(d);
                }
            }

            SyncFromSetupStateDisplays();
        }
        catch
        {
            DisplayItems.Clear();
            OnPropertyChanged(nameof(HasDisplays));
        }
    }

    private void SyncFromSetupStateDisplays()
    {
        if (_setupState == null) return;

        _isSyncing = true;
        try
        {
            foreach (var old in DisplayItems)
            {
                old.PropertyChanged -= OnDisplayItemPropertyChanged;
            }
            DisplayItems.Clear();
            int idx = DeviceType == DeviceType.Laptop ? 2 : 1;
            foreach (var d in _setupState.Displays)
            {
                int currentIdx = d.IsPrimary && DeviceType == DeviceType.Laptop ? 1 : idx++;
                var item = DisplayItemViewModel.FromDisplayInfo(d, displayIndex: currentIdx);
                if (_settingsService != null &&
                    !string.IsNullOrEmpty(item.HardwareId) &&
                    _settingsService.CurrentSettings.DisplayPlacements.TryGetValue(item.HardwareId, out var saved))
                {
                    item.Placement = saved;
                }
                item.PropertyChanged += OnDisplayItemPropertyChanged;
                DisplayItems.Add(item);
            }
            OnPropertyChanged(nameof(HasDisplays));
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void OnDisplayItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DisplayItemViewModel.Placement) ||
            sender is not DisplayItemViewModel item ||
            _settingsService == null ||
            string.IsNullOrEmpty(item.HardwareId))
        {
            return;
        }

        var settings = _settingsService.CurrentSettings;
        if (item.Placement == DisplayPlacement.Auto)
        {
            settings.DisplayPlacements.Remove(item.HardwareId);
        }
        else
        {
            settings.DisplayPlacements[item.HardwareId] = item.Placement;
        }
        _ = _settingsService.SaveSettingsAsync(settings);
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
        SyncFromSetupStateDisplays();
    }

    partial void OnSelectedLaptopScreenSizeChanged(double value)
    {
        if (_setupState != null && Math.Abs(_setupState.LaptopScreenSize - value) > 0.001)
        {
            _setupState.LaptopScreenSize = value;
        }
    }

    [RelayCommand]
    public void SetLaptopScreenSize(object? param)
    {
        if (param is double d)
        {
            SelectedLaptopScreenSize = d;
        }
        else if (param != null && double.TryParse(param.ToString(), out double parsed))
        {
            SelectedLaptopScreenSize = parsed;
        }
    }

    [RelayCommand]
    public void SetWallpaperPreset(string preset)
    {
        SelectedWallpaperPreset = preset;
        WallpaperBrush = preset switch
        {
            "Dusk" => DuskBrush,
            "Dune" => DuneBrush,
            "Mist" => MistBrush,
            "Graphite" => GraphiteBrush,
            _ => GraphiteBrush
        };

        if (_setupState != null)
        {
            _isSyncing = true;
            try
            {
                _setupState.WallpaperBrush = WallpaperBrush;
                _setupState.SelectedWallpaperPreset = SelectedWallpaperPreset;
            }
            finally
            {
                _isSyncing = false;
            }
        }
    }

    [RelayCommand]
    public async Task UseSystemWallpaperAsync(CancellationToken ct = default)
    {
        if (_wallpaperService == null)
        {
            SetWallpaperPreset("Graphite");
            return;
        }

        try
        {
            var path = await _wallpaperService.GetCurrentWallpaperPathAsync(ct).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                await using var stream = File.OpenRead(path);
                SetCustomWallpaper(stream);
                SelectedWallpaperPreset = "System";

                if (_setupState != null)
                {
                    _isSyncing = true;
                    try
                    {
                        _setupState.WallpaperBrush = WallpaperBrush;
                        _setupState.SelectedWallpaperPreset = "System";
                    }
                    finally
                    {
                        _isSyncing = false;
                    }
                }
            }
            else
            {
                SetWallpaperPreset("Graphite");
            }
        }
        catch
        {
            SetWallpaperPreset("Graphite");
        }
    }

    [RelayCommand]
    public async Task UploadWallpaperAsync()
    {
        try
        {
            TopLevel? topLevel = null;
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
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
                _currentImageBitmap?.Dispose();
                _currentImageBitmap = null;
                WallpaperBrush = new SolidColorBrush(Color.Parse("#252830"));
            }
            SelectedWallpaperPreset = "Upload";

            if (_setupState != null)
            {
                _setupState.WallpaperBrush = WallpaperBrush;
                _setupState.SelectedWallpaperPreset = "Upload";
            }
        }
        catch
        {
            // Ignore image parse errors
        }
    }

    private void OnSetupStateChanged(object? sender, EventArgs e)
    {
        if (_setupState == null || _isSyncing) return;

        DeviceType = _setupState.DeviceType;
        SelectedLaptopScreenSize = _setupState.LaptopScreenSize;
        WallpaperBrush = _setupState.WallpaperBrush ?? GraphiteBrush;
        SelectedWallpaperPreset = _setupState.SelectedWallpaperPreset;
        SyncFromSetupStateDisplays();
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
