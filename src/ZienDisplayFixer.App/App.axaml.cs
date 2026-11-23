namespace ZienDisplayFixer.App;

using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Microsoft.Extensions.DependencyInjection;
using ZienDisplayFixer.App.Services;
using ZienDisplayFixer.App.ViewModels;
using ZienDisplayFixer.App.Views;
using ZienDisplayFixer.Core.Interfaces;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }
    public static bool IsExplicitExit { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddDisplayFixerServices();

        var provider = services.BuildServiceProvider();
        Services = provider;

        if (provider.GetService<ISettingsService>() is { } settingsService)
        {
            _ = settingsService.LoadSettingsAsync();
        }

        // Initialize background hot-plug monitor singleton
        _ = provider.GetService<DisplayHotPlugMonitor>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            bool startMinimized = desktop.Args?.Any(arg => string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase)) == true
                || Environment.GetCommandLineArgs().Any(arg => string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase));

            var mainVm = provider.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow
            {
                DataContext = mainVm,
                StartMinimized = startMinimized
            };

            desktop.MainWindow = mainWindow;

            SetupTrayIcon(desktop, provider);

            if (!startMinimized)
            {
                mainWindow.Show();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop, IServiceProvider provider)
    {
        try
        {
            var trayIcon = new TrayIcon
            {
                ToolTipText = "ZIEN Display Fixer",
                IsVisible = true
            };

            try
            {
                var iconUri = new Uri("avares://ZienDisplayFixer.App/Assets/avalonia-logo.ico");
                using var stream = AssetLoader.Open(iconUri);
                trayIcon.Icon = new WindowIcon(stream);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load tray icon asset: {ex.Message}");
            }

            var menu = new NativeMenu();

            var fixNowItem = new NativeMenuItem("Fix now");
            fixNowItem.Click += async (s, e) =>
            {
                ShowMainWindow(desktop);
                if (provider.GetService<HomeViewModel>() is { } homeVm)
                {
                    if (homeVm.CanFixScreens)
                    {
                        await homeVm.FixScreensAsync();
                    }
                }
                else if (provider.GetService<DisplayHotPlugMonitor>() is { } monitor)
                {
                    await monitor.TriggerHotPlugCheckAsync();
                }
            };

            var openItem = new NativeMenuItem("Open");
            openItem.Click += (s, e) => ShowMainWindow(desktop);

            var quitItem = new NativeMenuItem("Quit");
            quitItem.Click += (s, e) =>
            {
                IsExplicitExit = true;
                desktop.Shutdown();
            };

            menu.Items.Add(fixNowItem);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(openItem);
            menu.Items.Add(quitItem);

            trayIcon.Menu = menu;
            trayIcon.Clicked += (s, e) => ShowMainWindow(desktop);

            var icons = new TrayIcons { trayIcon };
            TrayIcon.SetIcons(this, icons);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize tray icon: {ex.Message}");
        }
    }

    public static void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (desktop.MainWindow is { } window)
        {
            window.Show();
            window.WindowState = WindowState.Normal;
            window.ShowInTaskbar = true;
            window.Activate();
        }
    }
}
