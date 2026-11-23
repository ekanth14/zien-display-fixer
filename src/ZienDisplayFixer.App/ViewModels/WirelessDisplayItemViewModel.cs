namespace ZienDisplayFixer.App.ViewModels;

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZienDisplayFixer.Core.Models;

public partial class WirelessDisplayItemViewModel : ObservableObject
{
    public string Id { get; init; } = Guid.NewGuid().ToString();

    public string Name { get; init; } = "Wireless Display";

    public double DiagonalInches { get; init; } = 55.0;

    public string SizeText => $"{DiagonalInches:F0}\"";

    [ObservableProperty]
    private bool _isConnected;

    public IRelayCommand? ConnectCommand { get; set; }

    public WirelessDisplayItemViewModel() { }

    public WirelessDisplayItemViewModel(WirelessDisplayDevice device, Action<WirelessDisplayItemViewModel>? onConnect = null)
    {
        Id = device.Id;
        Name = device.Name;
        DiagonalInches = device.DiagonalInches > 0 ? device.DiagonalInches : 55.0;
        _isConnected = device.IsConnected;

        if (onConnect != null)
        {
            ConnectCommand = new RelayCommand(() => onConnect(this));
        }
    }
}

