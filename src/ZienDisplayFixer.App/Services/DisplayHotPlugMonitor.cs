namespace ZienDisplayFixer.App.Services;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ZienDisplayFixer.App.ViewModels;
using ZienDisplayFixer.Core.Engine;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public class DisplayHotPlugMonitor : IDisposable
{
    private readonly IDisplayService _displayService;
    private readonly ISettingsService _settingsService;
    private readonly IEdidReader? _edidReader;
    private readonly HomeViewModel? _homeViewModel;
    private readonly object _lock = new();

    private CancellationTokenSource? _debounceCts;
    private bool _isDisposed;
    private bool _isInternalFixing;

    public int DebounceDelayMs { get; set; } = 1500;

    public bool IsFixing => _isInternalFixing || (_homeViewModel?.IsFixing == true);

    public event EventHandler? HotPlugDetected;
    public event EventHandler? AutoFixCompleted;

    public DisplayHotPlugMonitor(
        IDisplayService displayService,
        ISettingsService settingsService,
        IEdidReader? edidReader = null,
        HomeViewModel? homeViewModel = null)
    {
        _displayService = displayService ?? throw new ArgumentNullException(nameof(displayService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _edidReader = edidReader;
        _homeViewModel = homeViewModel;

        _displayService.DisplaysChanged += OnDisplaysChanged;
    }

    private void OnDisplaysChanged(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            if (_isDisposed) return;

            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(DebounceDelayMs, token).ConfigureAwait(false);
                    if (!token.IsCancellationRequested)
                    {
                        await HandleHotPlugAsync(token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Debounce cancelled by subsequent hot-plug event
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Hot-plug monitor error: {ex.Message}");
                }
            }, token);
        }
    }

    public async Task TriggerHotPlugCheckAsync(CancellationToken ct = default)
    {
        await HandleHotPlugAsync(ct).ConfigureAwait(false);
    }

    public async Task HandleHotPlugAsync(CancellationToken ct = default)
    {
        if (!_settingsService.CurrentSettings.FixAutomaticallyOnConnect)
        {
            return;
        }

        if (IsFixing || _homeViewModel?.IsConfirmationVisible == true)
        {
            return;
        }

        HotPlugDetected?.Invoke(this, EventArgs.Empty);

        if (_homeViewModel != null)
        {
            RunOnUIThread(() =>
            {
                if (_homeViewModel.CanFixScreens)
                {
                    _ = _homeViewModel.FixScreensAsync();
                }
            });
            AutoFixCompleted?.Invoke(this, EventArgs.Empty);
            return;
        }

        _isInternalFixing = true;
        try
        {
            var rawDisplays = await _displayService.GetDisplaysAsync(ct).ConfigureAwait(false);
            if (rawDisplays.Count == 0) return;

            var modesToApply = new List<(string DisplayId, DisplayMode BestMode)>();

            foreach (var display in rawDisplays)
            {
                var modes = await _displayService.GetModesAsync(display.Id, ct).ConfigureAwait(false);
                var best = BestModePicker.PickBestMode(modes, display);
                if (best != null)
                {
                    bool needsUpdate = display.CurrentMode == null ||
                                       display.CurrentMode.Width != best.Width ||
                                       display.CurrentMode.Height != best.Height ||
                                       Math.Abs(display.CurrentMode.RefreshHz - best.RefreshHz) >= 0.5;

                    if (needsUpdate)
                    {
                        modesToApply.Add((display.Id, best));
                    }
                }
            }

            if (modesToApply.Count == 0) return;

            foreach (var (displayId, targetMode) in modesToApply)
            {
                bool testPassed = await _displayService.TestModeAsync(displayId, targetMode, ct).ConfigureAwait(false);
                if (!testPassed) return;
            }

            foreach (var (displayId, targetMode) in modesToApply)
            {
                await _displayService.ApplyModeAsync(displayId, targetMode, ct).ConfigureAwait(false);
            }

            AutoFixCompleted?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _isInternalFixing = false;
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
        lock (_lock)
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _displayService.DisplaysChanged -= OnDisplaysChanged;
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _debounceCts = null;
            }
        }
    }
}

