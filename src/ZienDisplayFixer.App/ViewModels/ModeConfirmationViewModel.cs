namespace ZienDisplayFixer.App.ViewModels;

using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class ModeConfirmationViewModel : ViewModelBase
{
    private readonly DispatcherTimer? _timer;
    private bool _isCompleted;

    [ObservableProperty]
    private int _remainingSeconds = 12;

    public Action? OnKeep { get; set; }
    public Action? OnRevert { get; set; }

    public bool IsTimerRunning => _timer?.IsEnabled ?? false;

    public ModeConfirmationViewModel(int initialSeconds = 12, bool startTimer = true)
    {
        _remainingSeconds = initialSeconds;

        if (startTimer)
        {
            try
            {
                _timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _timer.Tick += (s, e) => Tick();
                _timer.Start();
            }
            catch
            {
                // In headless testing or environments where Dispatcher is uninitialized
            }
        }
    }

    public void Tick()
    {
        if (_isCompleted)
        {
            return;
        }

        if (RemainingSeconds <= 1)
        {
            _isCompleted = true;
            StopTimer();
            RemainingSeconds = 0;
            OnRevert?.Invoke();
        }
        else
        {
            RemainingSeconds--;
        }
    }

    [RelayCommand]
    public void Keep()
    {
        if (_isCompleted)
        {
            return;
        }

        _isCompleted = true;
        StopTimer();
        OnKeep?.Invoke();
    }

    [RelayCommand]
    public void Revert()
    {
        if (_isCompleted)
        {
            return;
        }

        _isCompleted = true;
        StopTimer();
        OnRevert?.Invoke();
    }

    public void StopTimer()
    {
        _timer?.Stop();
    }
}
