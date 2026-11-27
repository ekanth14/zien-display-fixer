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
            _isInternalFixing = true;
            try
            {
                await RunOnUIThreadAsync(async () =>
                {
                    if (_homeViewModel.CanFixScreens)
                    {
                        await _homeViewModel.FixScreensAsync().ConfigureAwait(true);
                    }
                }).ConfigureAwait(false);
            }
            finally
            {
                _isInternalFixing = false;
            }

            AutoFixCompleted?.Invoke(this, EventArgs.Empty);
            return;
        }

        _isInternalFixing = true;
        try
        {
            var rawDisplays = await _displayService.GetDisplaysAsync(ct).ConfigureAwait(false);
            if (rawDisplays.Count == 0) return;

            var modesById = new Dictionary<string, IReadOnlyList<DisplayMode>>(StringComparer.OrdinalIgnoreCase);
            foreach (var display in rawDisplays)
            {
                modesById[display.Id] = await _displayService.GetModesAsync(display.Id, ct).ConfigureAwait(false);
            }

            var plan = DisplayFixPlanner.Plan(rawDisplays, modesById, _settingsService.CurrentSettings.DisplayPlacements);
            if (!plan.HasChanges) return;

            foreach (var target in plan.ModeChanges)
            {
                bool testPassed = await _displayService.TestModeAsync(target.DisplayId, target.Mode, ct).ConfigureAwait(false);
                if (!testPassed) return;
            }

            if (!await _displayService.ApplyConfigurationAsync(plan.Targets, ct).ConfigureAwait(false)) return;

            AutoFixCompleted?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _isInternalFixing = false;
        }
    }

    private static Task RunOnUIThreadAsync(Func<Task> action)
    {
        try
        {
            if (Avalonia.Application.Current != null && Dispatcher.UIThread is { } dispatcher && !dispatcher.CheckAccess())
            {
                var tcs = new TaskCompletionSource();
                dispatcher.Post(async () =>
                {
                    try
                    {
                        await action().ConfigureAwait(true);
                        tcs.TrySetResult();
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });
                return tcs.Task;
            }
        }
        catch
        {
            // Headless / unit testing fallback
        }

        return action();
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

