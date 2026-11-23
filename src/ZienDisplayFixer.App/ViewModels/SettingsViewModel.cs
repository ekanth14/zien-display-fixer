namespace ZienDisplayFixer.App.ViewModels;

using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ZienDisplayFixer.App.Services;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService? _settingsService;
    private readonly IDisplaySetupState? _setupState;
    private bool _isInitializing;

    public string Title => "Settings";

    [ObservableProperty]
    private bool _fixAutomaticallyOnConnect = true;

    [ObservableProperty]
    private bool _startAtLogin;

    [ObservableProperty]
    private bool _isAnimationEnabled = true;

    [ObservableProperty]
    private bool _isDemoModeEnabled = true;

    [ObservableProperty]
    private string? _statusMessage = "Settings are saved automatically.";

    public SettingsViewModel()
        : this(null, null)
    {
    }

    public SettingsViewModel(ISettingsService? settingsService, IDisplaySetupState? setupState)
    {
        _settingsService = settingsService;
        _setupState = setupState;

        _isInitializing = true;
        try
        {
            if (_settingsService != null)
            {
                var current = _settingsService.CurrentSettings;
                _fixAutomaticallyOnConnect = current.FixAutomaticallyOnConnect;
                _startAtLogin = current.StartAtLogin;
                _isAnimationEnabled = current.IsAnimationEnabled;
                _isDemoModeEnabled = current.IsDemoModeEnabled;
            }
            else
            {
                if (_setupState != null)
                {
                    _isAnimationEnabled = _setupState.IsAnimationEnabled;
                    _isDemoModeEnabled = _setupState.IsDemoModeEnabled;
                }
            }

            if (_setupState != null)
            {
                _setupState.IsAnimationEnabled = _isAnimationEnabled;
                _setupState.IsDemoModeEnabled = _isDemoModeEnabled;
            }
        }
        finally
        {
            _isInitializing = false;
        }

        if (_settingsService != null)
        {
            _settingsService.SettingsChanged += OnSettingsChangedFromService;
        }

        if (_setupState != null)
        {
            _setupState.SetupChanged += OnSetupStateChanged;
        }

        if (_settingsService != null)
        {
            _ = LoadSettingsAsync();
        }
    }

    private static void RunOnUIThread(Action action)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(action);
        }
    }

    private void OnSettingsChangedFromService(object? sender, AppSettings newSettings)
    {
        RunOnUIThread(() =>
        {
            if (_isInitializing) return;

            _isInitializing = true;
            try
            {
                FixAutomaticallyOnConnect = newSettings.FixAutomaticallyOnConnect;
                StartAtLogin = newSettings.StartAtLogin;
                IsAnimationEnabled = newSettings.IsAnimationEnabled;
                IsDemoModeEnabled = newSettings.IsDemoModeEnabled;

                if (_setupState != null)
                {
                    _setupState.IsAnimationEnabled = newSettings.IsAnimationEnabled;
                    _setupState.IsDemoModeEnabled = newSettings.IsDemoModeEnabled;
                }
            }
            finally
            {
                _isInitializing = false;
            }
        });
    }

    private void OnSetupStateChanged(object? sender, EventArgs e)
    {
        RunOnUIThread(() =>
        {
            if (_isInitializing || _setupState == null) return;

            if (IsAnimationEnabled != _setupState.IsAnimationEnabled ||
                IsDemoModeEnabled != _setupState.IsDemoModeEnabled)
            {
                _isInitializing = true;
                try
                {
                    IsAnimationEnabled = _setupState.IsAnimationEnabled;
                    IsDemoModeEnabled = _setupState.IsDemoModeEnabled;
                }
                finally
                {
                    _isInitializing = false;
                }

                OnSettingChanged();
            }
        });
    }

    public async Task LoadSettingsAsync(CancellationToken ct = default)
    {
        if (_settingsService == null) return;

        var loaded = await _settingsService.LoadSettingsAsync(ct).ConfigureAwait(false);
        RunOnUIThread(() =>
        {
            _isInitializing = true;
            try
            {
                FixAutomaticallyOnConnect = loaded.FixAutomaticallyOnConnect;
                StartAtLogin = loaded.StartAtLogin;
                IsAnimationEnabled = loaded.IsAnimationEnabled;
                IsDemoModeEnabled = loaded.IsDemoModeEnabled;

                if (_setupState != null)
                {
                    _setupState.IsAnimationEnabled = loaded.IsAnimationEnabled;
                    _setupState.IsDemoModeEnabled = loaded.IsDemoModeEnabled;
                }
            }
            finally
            {
                _isInitializing = false;
            }
        });
    }

    public Task SaveCurrentSettingsAsync(CancellationToken ct = default)
    {
        var updated = new AppSettings
        {
            FixAutomaticallyOnConnect = FixAutomaticallyOnConnect,
            StartAtLogin = StartAtLogin,
            IsAnimationEnabled = IsAnimationEnabled,
            IsDemoModeEnabled = IsDemoModeEnabled
        };

        if (_settingsService != null)
        {
            return _settingsService.SaveSettingsAsync(updated, ct);
        }

        return Task.CompletedTask;
    }

    partial void OnFixAutomaticallyOnConnectChanged(bool value) => OnSettingChanged();
    partial void OnStartAtLoginChanged(bool value) => OnSettingChanged();

    partial void OnIsAnimationEnabledChanged(bool value)
    {
        if (_setupState != null && _setupState.IsAnimationEnabled != value)
        {
            _setupState.IsAnimationEnabled = value;
        }
        OnSettingChanged();
    }

    partial void OnIsDemoModeEnabledChanged(bool value)
    {
        if (_setupState != null && _setupState.IsDemoModeEnabled != value)
        {
            _setupState.IsDemoModeEnabled = value;
        }
        OnSettingChanged();
    }

    private void OnSettingChanged()
    {
        if (_isInitializing) return;

        _ = SaveCurrentSettingsAsync();
        StatusMessage = "Settings are saved automatically.";
    }
}
