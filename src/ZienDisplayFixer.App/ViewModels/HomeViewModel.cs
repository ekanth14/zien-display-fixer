namespace ZienDisplayFixer.App.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZienDisplayFixer.App.Services;
using ZienDisplayFixer.Core.Edid;
using ZienDisplayFixer.Core.Engine;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public partial class HomeViewModel : ViewModelBase, IDisposable
{
    private readonly IDisplayService? _displayService;
    private readonly IEdidReader? _edidReader;
    private readonly IScreenModeService? _screenModeService;
    private readonly IDisplaySetupState? _setupState;
    private bool _isDisposed;

    public string Title => "Home";

    public ObservableCollection<DisplayInfo> Displays { get; } = [];

    [ObservableProperty]
    private DeviceType _deviceType = DeviceType.Laptop;

    [ObservableProperty]
    private double _laptopScreenSize = 14.0;

    [ObservableProperty]
    private IBrush? _wallpaperBrush;

    [ObservableProperty]
    private bool _isAnimationEnabled = true;

    [ObservableProperty]
    private string _statusText = "Checking display configuration...";

    [ObservableProperty]
    private ScreenTopology _currentTopology = ScreenTopology.Extend;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanFixScreens))]
    [NotifyCanExecuteChangedFor(nameof(FixScreensCommand))]
    private bool _isConfirmationVisible;

    [ObservableProperty]
    private ModeConfirmationViewModel? _confirmation;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanFixScreens))]
    [NotifyCanExecuteChangedFor(nameof(FixScreensCommand))]
    private bool _isFixing;

    public bool CanFixScreens => !IsFixing && !IsConfirmationVisible;

    public HomeViewModel()
        : this(null, null, null, null)
    {
    }

    public HomeViewModel(
        IDisplayService? displayService,
        IEdidReader? edidReader,
        IScreenModeService? screenModeService,
        IDisplaySetupState? setupState = null)
    {
        _displayService = displayService;
        _edidReader = edidReader;
        _screenModeService = screenModeService;
        _setupState = setupState;

        if (_setupState != null)
        {
            _deviceType = _setupState.DeviceType;
            _laptopScreenSize = _setupState.LaptopScreenSize;
            _wallpaperBrush = _setupState.WallpaperBrush;
            _isAnimationEnabled = _setupState.IsAnimationEnabled;
            _setupState.SetupChanged += OnSetupStateChanged;
        }

        if (_displayService != null)
        {
            _displayService.DisplaysChanged += OnDisplaysChanged;
        }

        _ = LoadDisplaysAsync();
    }

    private void OnSetupStateChanged(object? sender, EventArgs e)
    {
        if (_setupState == null) return;
        RunOnUIThread(() =>
        {
            DeviceType = _setupState.DeviceType;
            LaptopScreenSize = _setupState.LaptopScreenSize;
            WallpaperBrush = _setupState.WallpaperBrush;
            IsAnimationEnabled = _setupState.IsAnimationEnabled;

            if (_setupState.IsDemoModeEnabled)
            {
                Displays.Clear();
                foreach (var d in _setupState.Displays)
                {
                    Displays.Add(d);
                }
            }
        });
    }

    partial void OnDeviceTypeChanged(DeviceType value)
    {
        if (_setupState != null && _setupState.DeviceType != value)
        {
            _setupState.DeviceType = value;
        }
    }

    partial void OnLaptopScreenSizeChanged(double value)
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

    partial void OnIsAnimationEnabledChanged(bool value)
    {
        if (_setupState != null && _setupState.IsAnimationEnabled != value)
        {
            _setupState.IsAnimationEnabled = value;
        }
    }

    private void OnDisplaysChanged(object? sender, EventArgs e)
    {
        RunOnUIThread(() => _ = LoadDisplaysAsync());
    }

    public async Task LoadDisplaysAsync(CancellationToken ct = default)
    {
        if (_setupState != null && _setupState.IsDemoModeEnabled && _setupState.Displays.Count > 0)
        {
            var demoList = _setupState.Displays.ToList();
            RunOnUIThread(() =>
            {
                Displays.Clear();
                foreach (var d in demoList)
                {
                    Displays.Add(d);
                }
            });
            await EvaluateOptimalStateAsync(demoList, ct).ConfigureAwait(false);
            return;
        }

        if (_displayService == null)
        {
            RunOnUIThread(() => StatusText = "No display service available");
            return;
        }

        try
        {
            RunOnUIThread(() => ErrorMessage = null);
            var rawDisplays = await _displayService.GetDisplaysAsync(ct).ConfigureAwait(false);
            var enriched = new List<DisplayInfo>();

            foreach (var display in rawDisplays)
            {
                var current = display;
                if (_edidReader != null && current.PhysicalWidthMm <= 0)
                {
                    try
                    {
                        var edidBytes = await _edidReader.ReadRawEdidAsync(current.Id, ct).ConfigureAwait(false);
                        if (edidBytes != null)
                        {
                            var parsed = EdidParser.Parse(edidBytes);
                            if (parsed != null)
                            {
                                double wMm = parsed.PhysicalWidthCm > 0 ? parsed.PhysicalWidthCm * 10 : current.PhysicalWidthMm;
                                double hMm = parsed.PhysicalHeightCm > 0 ? parsed.PhysicalHeightCm * 10 : current.PhysicalHeightMm;
                                string model = !string.IsNullOrWhiteSpace(parsed.ModelName) ? parsed.ModelName : current.Model;
                                string name = !string.IsNullOrWhiteSpace(parsed.ModelName) ? parsed.ModelName : current.Name;

                                current = current with
                                {
                                    PhysicalWidthMm = wMm,
                                    PhysicalHeightMm = hMm,
                                    Model = model,
                                    Name = name
                                };
                            }
                        }
                    }
                    catch
                    {
                        // Fall back to existing display info
                    }
                }
                enriched.Add(current);
            }

            RunOnUIThread(() =>
            {
                Displays.Clear();
                foreach (var d in enriched)
                {
                    Displays.Add(d);
                }

                if (_setupState != null && !_setupState.IsDemoModeEnabled)
                {
                    _setupState.Displays.Clear();
                    foreach (var d in enriched)
                    {
                        _setupState.Displays.Add(d);
                    }
                }
            });

            if (_screenModeService != null)
            {
                try
                {
                    var topo = await _screenModeService.GetCurrentTopologyAsync(ct).ConfigureAwait(false);
                    RunOnUIThread(() => CurrentTopology = topo);
                }
                catch
                {
                    // Keep existing topology
                }
            }

            await EvaluateOptimalStateAsync(enriched, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            RunOnUIThread(() => StatusText = $"Error reading displays: {ex.Message}");
        }
    }

    private async Task EvaluateOptimalStateAsync(IReadOnlyList<DisplayInfo> displays, CancellationToken ct)
    {
        if (displays.Count == 0)
        {
            RunOnUIThread(() => StatusText = "No displays detected");
            return;
        }

        int improvableCount = 0;
        foreach (var display in displays)
        {
            bool isOptimal = false;
            if (_displayService != null)
            {
                try
                {
                    var modes = await _displayService.GetModesAsync(display.Id, ct).ConfigureAwait(false);
                    var best = BestModePicker.PickBestMode(modes, display);
                    if (best != null && display.CurrentMode != null)
                    {
                        isOptimal = display.CurrentMode.Width == best.Width &&
                                    display.CurrentMode.Height == best.Height &&
                                    Math.Abs(display.CurrentMode.RefreshHz - best.RefreshHz) < 0.5;
                    }
                    else if (modes.Count == 0)
                    {
                        isOptimal = display.CurrentMode != null &&
                                    !display.CurrentMode.IsInterlaced &&
                                    display.CurrentMode.RefreshHz >= 59.0 &&
                                    display.CurrentMode.Width >= 1280 &&
                                    display.CurrentMode.Height >= 720;
                    }
                }
                catch
                {
                    isOptimal = false;
                }
            }

            if (!isOptimal)
            {
                improvableCount++;
            }
        }

        RunOnUIThread(() =>
        {
            if (improvableCount > 0)
            {
                if (displays.Count == 1)
                {
                    StatusText = "Your display can be improved";
                }
                else
                {
                    StatusText = $"{improvableCount} of {displays.Count} displays can be improved";
                }
            }
            else
            {
                if (DeviceType == DeviceType.Laptop && displays.Count == 1)
                {
                    StatusText = "No external displays connected";
                }
                else
                {
                    StatusText = "All displays are at their best settings";
                }
            }
        });
    }

    [RelayCommand(CanExecute = nameof(CanFixScreens))]
    public async Task FixScreensAsync()
    {
        if (_displayService == null || Displays.Count == 0 || IsFixing)
        {
            return;
        }

        RunOnUIThread(() =>
        {
            IsFixing = true;
            ErrorMessage = null;
        });

        try
        {
            var modesToApply = new List<(DisplayInfo Display, DisplayMode TargetMode)>();

            foreach (var display in Displays)
            {
                var modes = await _displayService.GetModesAsync(display.Id).ConfigureAwait(false);
                var best = BestModePicker.PickBestMode(modes, display);
                if (best != null)
                {
                    bool needsUpdate = display.CurrentMode == null ||
                                       display.CurrentMode.Width != best.Width ||
                                       display.CurrentMode.Height != best.Height ||
                                       Math.Abs(display.CurrentMode.RefreshHz - best.RefreshHz) >= 0.5;

                    if (needsUpdate)
                    {
                        modesToApply.Add((display, best));
                    }
                }
            }

            if (modesToApply.Count == 0)
            {
                RunOnUIThread(() => StatusText = "All displays are at their best settings");
                return;
            }

            // Test mode for each display first
            foreach (var (display, targetMode) in modesToApply)
            {
                bool testPassed = await _displayService.TestModeAsync(display.Id, targetMode).ConfigureAwait(false);
                if (!testPassed)
                {
                    RunOnUIThread(() => ErrorMessage = $"Mode {targetMode.Width}x{targetMode.Height} rejected by display driver.");
                    return;
                }
            }

            // Apply all tested modes in batch
            foreach (var (display, targetMode) in modesToApply)
            {
                await _displayService.ApplyModeAsync(display.Id, targetMode).ConfigureAwait(false);
            }

            // Launch 12-second fail-safe confirmation
            RunOnUIThread(() =>
            {
                var confirmation = new ModeConfirmationViewModel(initialSeconds: 12)
                {
                    OnKeep = () =>
                    {
                        IsConfirmationVisible = false;
                        Confirmation = null;
                        StatusText = "All displays are at their best settings";
                        _ = LoadDisplaysAsync();
                    },
                    OnRevert = () =>
                    {
                        IsConfirmationVisible = false;
                        Confirmation = null;
                        _ = RevertPreviousModesAsync();
                    }
                };

                Confirmation = confirmation;
                IsConfirmationVisible = true;
            });
        }
        catch (Exception ex)
        {
            RunOnUIThread(() => ErrorMessage = $"Failed to apply optimal display modes: {ex.Message}");
        }
        finally
        {
            RunOnUIThread(() => IsFixing = false);
        }
    }

    private async Task RevertPreviousModesAsync()
    {
        if (_displayService == null) return;

        try
        {
            await _displayService.RestorePreviousModesAsync().ConfigureAwait(false);
            await LoadDisplaysAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            RunOnUIThread(() => ErrorMessage = $"Failed to revert display modes: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task SetTopologyAsync(object? param)
    {
        ScreenTopology topology;
        if (param is ScreenTopology st)
        {
            topology = st;
        }
        else if (param is string str && Enum.TryParse<ScreenTopology>(str, true, out var parsed))
        {
            topology = parsed;
        }
        else
        {
            return;
        }

        CurrentTopology = topology;

        if (_screenModeService != null)
        {
            try
            {
                await _screenModeService.SetTopologyAsync(topology).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                RunOnUIThread(() => ErrorMessage = $"Failed to switch screen topology: {ex.Message}");
            }
        }
    }

    private static void RunOnUIThread(Action action)
    {
        try
        {
            if (Avalonia.Application.Current != null && Dispatcher.UIThread is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.Post(action);
                return;
            }
        }
        catch
        {
            // Headless / unit testing fallback
        }

        action();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            if (_setupState != null)
            {
                _setupState.SetupChanged -= OnSetupStateChanged;
            }
            if (_displayService != null)
            {
                _displayService.DisplaysChanged -= OnDisplaysChanged;
            }
            Confirmation?.StopTimer();
        }
    }
}
