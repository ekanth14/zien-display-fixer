namespace ZienDisplayFixer.App.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZienDisplayFixer.App.Services;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public partial class WirelessViewModel : ViewModelBase, IDisposable
{
    private readonly IWirelessDisplayLauncher? _launcher;
    private readonly IDisplaySetupState? _setupState;

    public string Title => "Wireless";

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSupported = true;

    [ObservableProperty]
    private bool _autoOpenPickerOnSearch;

    public ObservableCollection<WirelessDisplayItemViewModel> DiscoveredDevices { get; } = [];

    public bool HasDiscoveredDevices => DiscoveredDevices.Count > 0;

    public WirelessViewModel()
        : this(null, null)
    {
    }

    public WirelessViewModel(
        IWirelessDisplayLauncher? launcher,
        IDisplaySetupState? setupState = null)
    {
        _launcher = launcher;
        _setupState = setupState;

        _isSupported = _launcher?.IsSupported ?? true;

        DiscoveredDevices.CollectionChanged += OnDiscoveredDevicesCollectionChanged;

        if (_setupState != null)
        {
            _setupState.SetupChanged += OnSetupStateChanged;
        }

        // Initialize devices with default scan
        _ = SearchAgainAsync();
    }

    private void OnDiscoveredDevicesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasDiscoveredDevices));
    }

    private void OnSetupStateChanged(object? sender, EventArgs e)
    {
        SyncConnectedStatusFromSetupState();
    }

    private void SyncConnectedStatusFromSetupState()
    {
        if (_setupState == null) return;

        foreach (var device in DiscoveredDevices)
        {
            bool isConnected = _setupState.Displays.Any(d =>
                string.Equals(d.Id, device.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(d.Name, device.Name, StringComparison.OrdinalIgnoreCase));
            device.IsConnected = isConnected;
        }
    }

    [RelayCommand]
    public async Task SearchAgainAsync(object? param = null)
    {
        bool openPicker = AutoOpenPickerOnSearch;
        if (param is bool b)
        {
            openPicker = b;
        }
        else if (param is string s && bool.TryParse(s, out bool parsed))
        {
            openPicker = parsed;
        }

        IsScanning = true;
        StatusMessage = "Searching for nearby wireless displays...";

        try
        {
            if (openPicker && _launcher != null && _launcher.IsSupported)
            {
                await _launcher.OpenWirelessDisplayPickerAsync();
            }

            IReadOnlyList<WirelessDisplayDevice> devices;
            if (_launcher != null)
            {
                devices = await _launcher.ScanNearbyAsync();
            }
            else
            {
                devices =
                [
                    new WirelessDisplayDevice("w-living-room", "Living room TV", false, 55.0),
                    new WirelessDisplayDevice("w-bedroom", "Bedroom TV", false, 43.0),
                    new WirelessDisplayDevice("w-office", "Office display", false, 27.0)
                ];
            }

            DiscoveredDevices.Clear();
            foreach (var d in devices)
            {
                var item = new WirelessDisplayItemViewModel(d, device => _ = ConnectAsync(device));
                if (_setupState != null && _setupState.Displays.Any(x =>
                    string.Equals(x.Id, d.Id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.Name, d.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    item.IsConnected = true;
                }
                DiscoveredDevices.Add(item);
            }

            StatusMessage = DiscoveredDevices.Count > 0
                ? $"Found {DiscoveredDevices.Count} display(s)."
                : "No wireless displays found nearby.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    public async Task ConnectAsync(object? param = null)
    {
        WirelessDisplayItemViewModel? item = param as WirelessDisplayItemViewModel
            ?? DiscoveredDevices.FirstOrDefault(d => !d.IsConnected)
            ?? DiscoveredDevices.FirstOrDefault();

        if (_launcher != null && _launcher.IsSupported)
        {
            try
            {
                await _launcher.OpenWirelessDisplayPickerAsync();
            }
            catch
            {
                // Fallback / ignore error if picker cannot open
            }
        }

        if (item != null)
        {
            item.IsConnected = true;

            if (_setupState != null && _setupState.IsDemoModeEnabled)
            {
                bool exists = _setupState.Displays.Any(d =>
                    string.Equals(d.Id, item.Id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(d.Name, item.Name, StringComparison.OrdinalIgnoreCase));

                if (!exists && _setupState.Displays.Count < 8)
                {
                    var (wMm, hMm) = DisplayItemViewModel.CalculateDimensions(item.DiagonalInches, "16:9");
                    var display = new DisplayInfo(
                        id: item.Id,
                        name: item.Name,
                        model: item.Name,
                        isPrimary: false,
                        positionX: 0,
                        positionY: 0,
                        currentMode: new DisplayMode(3840, 2160, 60.0, false),
                        physicalWidthMm: wMm,
                        physicalHeightMm: hMm,
                        connection: ConnectionType.WiFi);

                    _setupState.Displays.Add(display);
                }
            }

            StatusMessage = $"Connected to {item.Name}.";
        }
    }

    [RelayCommand]
    public async Task OpenSystemSettingsAsync(CancellationToken ct = default)
    {
        if (_launcher != null && _launcher.IsSupported)
        {
            try
            {
                await _launcher.OpenWirelessDisplayPickerAsync(ct);
                StatusMessage = "Opened system wireless display settings.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to open settings: {ex.Message}";
            }
        }
        else
        {
            StatusMessage = "Wireless display settings are not supported on this platform.";
        }
    }

    public void Dispose()
    {
        DiscoveredDevices.CollectionChanged -= OnDiscoveredDevicesCollectionChanged;
        if (_setupState != null)
        {
            _setupState.SetupChanged -= OnSetupStateChanged;
        }
    }
}
