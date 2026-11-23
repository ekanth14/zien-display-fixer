using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZienDisplayFixer.App.Services;
using ZienDisplayFixer.App.ViewModels;

namespace ZienDisplayFixer.Tests.ViewModels;

public class MainViewModelTests
{
    [Fact]
    public void InitialState_DefaultConstructor_SetsHomePageAndZeroIndex()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.CurrentPage);
        Assert.IsType<HomeViewModel>(vm.CurrentPage);
        Assert.Equal(0, vm.SelectedPageIndex);
        Assert.Equal("0 displays", vm.DisplaySummaryText);
    }

    [Fact]
    public void NavigateCommand_SwitchesPagesAndIndices()
    {
        var vm = new MainViewModel();

        // Navigate to Displays (1)
        vm.NavigateCommand.Execute(1);
        Assert.Equal(1, vm.SelectedPageIndex);
        Assert.IsType<DisplaysViewModel>(vm.CurrentPage);
        Assert.Same(vm.Displays, vm.CurrentPage);

        // Navigate to Wireless (2)
        vm.NavigateCommand.Execute(2);
        Assert.Equal(2, vm.SelectedPageIndex);
        Assert.IsType<WirelessViewModel>(vm.CurrentPage);
        Assert.Same(vm.Wireless, vm.CurrentPage);

        // Navigate to Settings (3)
        vm.NavigateCommand.Execute(3);
        Assert.Equal(3, vm.SelectedPageIndex);
        Assert.IsType<SettingsViewModel>(vm.CurrentPage);
        Assert.Same(vm.Settings, vm.CurrentPage);

        // Navigate back to Home (0)
        vm.NavigateCommand.Execute(0);
        Assert.Equal(0, vm.SelectedPageIndex);
        Assert.IsType<HomeViewModel>(vm.CurrentPage);
        Assert.Same(vm.Home, vm.CurrentPage);
    }

    [Fact]
    public void PageNavigationCommand_ExposesNavigateCommandCorrectly()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.PageNavigationCommand);
        vm.PageNavigationCommand.Execute(2);

        Assert.Equal(2, vm.SelectedPageIndex);
        Assert.IsType<WirelessViewModel>(vm.CurrentPage);
    }

    [Fact]
    public void SelectedPageIndex_DirectAssignment_SwitchesCurrentPage()
    {
        var vm = new MainViewModel();

        vm.SelectedPageIndex = 1;
        Assert.IsType<DisplaysViewModel>(vm.CurrentPage);

        vm.SelectedPageIndex = 3;
        Assert.IsType<SettingsViewModel>(vm.CurrentPage);

        // Out of range defaults to Home
        vm.SelectedPageIndex = 99;
        Assert.IsType<HomeViewModel>(vm.CurrentPage);
    }

    [Fact]
    public void CurrentPage_DirectAssignment_SwitchesSelectedPageIndex()
    {
        var vm = new MainViewModel();

        vm.CurrentPage = vm.Displays;
        Assert.Equal(1, vm.SelectedPageIndex);

        vm.CurrentPage = vm.Wireless;
        Assert.Equal(2, vm.SelectedPageIndex);

        vm.CurrentPage = vm.Settings;
        Assert.Equal(3, vm.SelectedPageIndex);

        vm.CurrentPage = vm.Home;
        Assert.Equal(0, vm.SelectedPageIndex);
    }

    [Fact]
    public void DisplaySummaryText_CanBeChangedAndNotifies()
    {
        var vm = new MainViewModel();
        var raised = false;

        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.DisplaySummaryText))
                raised = true;
        };

        vm.DisplaySummaryText = "2 displays · 1 wireless";

        Assert.True(raised);
        Assert.Equal("2 displays · 1 wireless", vm.DisplaySummaryText);
    }

    [Fact]
    public void ServiceRegistration_RegistersAndResolvesMainViewModel()
    {
        var services = new ServiceCollection();
        services.AddDisplayFixerServices();

        var provider = services.BuildServiceProvider();

        var mainVm = provider.GetService<MainViewModel>();
        Assert.NotNull(mainVm);
        Assert.NotNull(mainVm.Home);
        Assert.NotNull(mainVm.Displays);
        Assert.NotNull(mainVm.Wireless);
        Assert.NotNull(mainVm.Settings);
        Assert.Same(mainVm.Home, mainVm.CurrentPage);
    }
}
