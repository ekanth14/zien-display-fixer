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
    private readonly IAutostartService? _autostartService;
    private bool _isInitializing;

    public string Title => "Settings";

    [ObservableProperty]
    private bool _fixAutomaticallyOnConnect = true;

    [ObservableProperty]
    private bool _startAtLogin;

    [ObservableProperty]
    private bool _isAnimationEnabled = true;

    [ObservableProperty]
    private string? _statusMessage = "Settings are saved automatically.";

    public SettingsViewModel()
        : this(null, null, null)
    {
    }

    public SettingsViewModel(
        ISettingsService? settingsService,
        IDisplaySetupState? setupState,
        IAutostartService? autostartService = null)
    {
        _settingsService = settingsService;
        _setupState = setupState;
        _autostartService = autostartService;

        _isInitializing = true;
        try
        {
            if (_settingsService != null)
            {
                var current = _settingsService.CurrentSettings;
                _fixAutomaticallyOnConnect = current.FixAutomaticallyOnConnect;
                _startAtLogin = current.StartAtLogin;
                _isAnimationEnabled = current.IsAnimationEnabled;
            }
            else if (_setupState != null)
            {
                _isAnimationEnabled = _setupState.IsAnimationEnabled;
            }

            if (_autostartService != null)
            {
                _startAtLogin = _autostartService.IsAutostartEnabled();
            }

            if (_setupState != null)
            {
                _setupState.IsAnimationEnabled = _isAnimationEnabled;
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
        try
        {
            if (Avalonia.Application.Current != null && Avalonia.Threading.Dispatcher.UIThread is { } dispatcher && !dispatcher.CheckAccess())
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

                if (_setupState != null)
                {
                    _setupState.IsAnimationEnabled = newSettings.IsAnimationEnabled;
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

            if (IsAnimationEnabled != _setupState.IsAnimationEnabled)
            {
                _isInitializing = true;
                try
                {
                    IsAnimationEnabled = _setupState.IsAnimationEnabled;
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

                if (_setupState != null)
                {
                    _setupState.IsAnimationEnabled = loaded.IsAnimationEnabled;
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
            IsAnimationEnabled = IsAnimationEnabled
        };

        if (_settingsService != null)
        {
            return _settingsService.SaveSettingsAsync(updated, ct);
        }

        return Task.CompletedTask;
    }

    partial void OnFixAutomaticallyOnConnectChanged(bool value) => OnSettingChanged();

    partial void OnStartAtLoginChanged(bool value)
    {
        if (!_isInitializing)
        {
            try
            {
                _autostartService?.SetAutostart(value);
            }
            catch
            {
                // Graceful fallback
            }
        }
        OnSettingChanged();
    }

    partial void OnIsAnimationEnabledChanged(bool value)
    {
        if (_setupState != null && _setupState.IsAnimationEnabled != value)
        {
            _setupState.IsAnimationEnabled = value;
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
