using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ZienDisplayFixer.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly HomeViewModel _homeViewModel;
    private readonly DisplaysViewModel _displaysViewModel;
    private readonly WirelessViewModel _wirelessViewModel;
    private readonly SettingsViewModel _settingsViewModel;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private int _selectedPageIndex;

    [ObservableProperty]
    private string _displaySummaryText = "0 displays";

    public MainViewModel()
        : this(new HomeViewModel(), new DisplaysViewModel(), new WirelessViewModel(), new SettingsViewModel())
    {
    }

    public MainViewModel(
        HomeViewModel homeViewModel,
        DisplaysViewModel displaysViewModel,
        WirelessViewModel wirelessViewModel,
        SettingsViewModel settingsViewModel)
    {
        _homeViewModel = homeViewModel;
        _displaysViewModel = displaysViewModel;
        _wirelessViewModel = wirelessViewModel;
        _settingsViewModel = settingsViewModel;

        _currentPage = _homeViewModel;
        _selectedPageIndex = 0;
    }

    public HomeViewModel Home => _homeViewModel;
    public DisplaysViewModel Displays => _displaysViewModel;
    public WirelessViewModel Wireless => _wirelessViewModel;
    public SettingsViewModel Settings => _settingsViewModel;

    [RelayCommand]
    private void Navigate(int pageIndex)
    {
        SelectedPageIndex = pageIndex;
    }

    public IRelayCommand<int> PageNavigationCommand => NavigateCommand;

    partial void OnSelectedPageIndexChanged(int value)
    {
        CurrentPage = value switch
        {
            0 => _homeViewModel,
            1 => _displaysViewModel,
            2 => _wirelessViewModel,
            3 => _settingsViewModel,
            _ => _homeViewModel
        };
    }

    partial void OnCurrentPageChanged(ViewModelBase value)
    {
        SelectedPageIndex = value switch
        {
            HomeViewModel => 0,
            DisplaysViewModel => 1,
            WirelessViewModel => 2,
            SettingsViewModel => 3,
            _ => 0
        };
    }
}
