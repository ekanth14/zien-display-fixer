namespace ZienDisplayFixer.App.Views;

using System;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using ZienDisplayFixer.Core.Interfaces;

public partial class MainWindow : Window
{
    public bool StartMinimized { get; set; }

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (StartMinimized)
        {
            StartMinimized = false;
            Hide();
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (App.IsExplicitExit)
        {
            base.OnClosing(e);
            return;
        }

        var settingsService = App.Services?.GetService<ISettingsService>();
        bool fixAuto = settingsService?.CurrentSettings.FixAutomaticallyOnConnect ?? true;

        if (fixAuto)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
