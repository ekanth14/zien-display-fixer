using System;
using Xunit;
using ZienDisplayFixer.App.ViewModels;

namespace ZienDisplayFixer.Tests.ViewModels;

public class ModeConfirmationViewModelTests
{
    [Fact]
    public void InitialState_DefaultSecondsIs12()
    {
        var vm = new ModeConfirmationViewModel(initialSeconds: 12, startTimer: false);

        Assert.Equal(12, vm.RemainingSeconds);
    }

    [Fact]
    public void Tick_DecrementsRemainingSeconds()
    {
        var vm = new ModeConfirmationViewModel(initialSeconds: 12, startTimer: false);

        vm.Tick();

        Assert.Equal(11, vm.RemainingSeconds);
    }

    [Fact]
    public void Tick_WhenRemainingSecondsIsOne_InvokesRevertAndSetsRemainingToZero()
    {
        var vm = new ModeConfirmationViewModel(initialSeconds: 1, startTimer: false);
        bool revertInvoked = false;
        vm.OnRevert = () => revertInvoked = true;

        vm.Tick();

        Assert.Equal(0, vm.RemainingSeconds);
        Assert.True(revertInvoked);
    }

    [Fact]
    public void KeepCommand_InvokesOnKeepAndStopsTimer()
    {
        var vm = new ModeConfirmationViewModel(initialSeconds: 12, startTimer: false);
        bool keepInvoked = false;
        vm.OnKeep = () => keepInvoked = true;

        vm.KeepCommand.Execute(null);

        Assert.True(keepInvoked);
        // After keep, ticks should not invoke revert even if simulated further
        vm.RemainingSeconds = 1;
        vm.Tick();
    }

    [Fact]
    public void RevertCommand_InvokesOnRevertAndStopsTimer()
    {
        var vm = new ModeConfirmationViewModel(initialSeconds: 12, startTimer: false);
        bool revertInvoked = false;
        vm.OnRevert = () => revertInvoked = true;

        vm.RevertCommand.Execute(null);

        Assert.True(revertInvoked);
    }
}
