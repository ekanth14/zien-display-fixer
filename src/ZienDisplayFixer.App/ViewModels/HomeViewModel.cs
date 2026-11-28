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
    private readonly ISettingsService? _settingsService;
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
        IDisplaySetupState? setupState = null,
        ISettingsService? settingsService = null)
    {
        _displayService = displayService;
        _edidReader = edidReader;
        _screenModeService = screenModeService;
        _setupState = setupState;
        _settingsService = settingsService;

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

        if (_settingsService != null)
        {
            _settingsService.SettingsChanged += OnSettingsChanged;
        }

        _ = LoadDisplaysAsync();
    }

    private void OnSettingsChanged(object? sender, AppSettings e) => OnDisplaysChanged(sender, EventArgs.Empty);

    private void OnSetupStateChanged(object? sender, EventArgs e)
    {
        if (_setupState == null) return;
        RunOnUIThread(() =>
        {
            DeviceType = _setupState.DeviceType;
            LaptopScreenSize = _setupState.LaptopScreenSize;
            WallpaperBrush = _setupState.WallpaperBrush;
            IsAnimationEnabled = _setupState.IsAnimationEnabled;
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
        RunOnUIThread(() =>
        {
            if (!IsConfirmationVisible && !IsFixing)
            {
                _ = LoadDisplaysAsync();
            }
        });
    }

    public async Task LoadDisplaysAsync(CancellationToken ct = default)
    {
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
                                    Name = name,
                                    NativeMode = current.NativeMode ?? parsed.PreferredMode
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

                if (_setupState != null)
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

        var (plan, modesById) = await BuildFixPlanAsync(displays, ct).ConfigureAwait(false);

        // Displays that expose no mode list can't be planned; judge them by their current mode instead.
        int unlistedButPoor = displays.Count(d =>
            modesById.GetValueOrDefault(d.Id)?.Count is null or 0 &&
            !(d.CurrentMode is { IsInterlaced: false } m && m.RefreshHz >= 59.0 && m.Width >= 1280 && m.Height >= 720));
        int improvableCount = plan.ModeChangeCount + unlistedButPoor;
        bool onlyLayoutNeedsFixing = improvableCount == 0 && plan.HasChanges;

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
            else if (onlyLayoutNeedsFixing)
            {
                StatusText = "Screen positions can be tidied up";
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

        var displays = Displays.ToList();

        try
        {
            var (plan, _) = await BuildFixPlanAsync(displays, CancellationToken.None).ConfigureAwait(false);

            if (!plan.HasChanges)
            {
                RunOnUIThread(() => StatusText = "All displays are at their best settings");
                return;
            }

            foreach (var target in plan.ModeChanges)
            {
                bool testPassed = await _displayService.TestModeAsync(target.DisplayId, target.Mode).ConfigureAwait(false);
                if (!testPassed)
                {
                    RunOnUIThread(() => ErrorMessage = $"Mode {target.Mode.Width}x{target.Mode.Height} rejected by display driver.");
                    return;
                }
            }

            bool applied = await _displayService.ApplyConfigurationAsync(plan.Targets).ConfigureAwait(false);
            if (!applied)
            {
                RunOnUIThread(() => ErrorMessage = "The new display settings could not be applied, so your previous setup is being restored.");
                _ = RevertPreviousModesAsync();
                return;
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

    private async Task<(DisplayFixPlan Plan, Dictionary<string, IReadOnlyList<DisplayMode>> ModesById)> BuildFixPlanAsync(
        IReadOnlyList<DisplayInfo> displays,
        CancellationToken ct)
    {
        var modesById = new Dictionary<string, IReadOnlyList<DisplayMode>>(StringComparer.OrdinalIgnoreCase);
        if (_displayService != null)
        {
            foreach (var display in displays)
            {
                try
                {
                    modesById[display.Id] = await _displayService.GetModesAsync(display.Id, ct).ConfigureAwait(false);
                }
                catch
                {
                    modesById[display.Id] = [];
                }
            }
        }

        var plan = DisplayFixPlanner.Plan(displays, modesById, _settingsService?.CurrentSettings.DisplayPlacements);
        return (plan, modesById);
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
            if (_settingsService != null)
            {
                _settingsService.SettingsChanged -= OnSettingsChanged;
            }
            Confirmation?.StopTimer();
        }
    }
}
