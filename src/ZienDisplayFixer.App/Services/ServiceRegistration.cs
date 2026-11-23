namespace ZienDisplayFixer.App.Services;

using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using ZienDisplayFixer.App.Services.Linux;
using ZienDisplayFixer.App.Services.MacOS;
using ZienDisplayFixer.App.Services.Windows;
using ZienDisplayFixer.App.ViewModels;
using ZienDisplayFixer.Core.Interfaces;

public static class ServiceRegistration
{
    public static IServiceCollection AddDisplayFixerServices(this IServiceCollection services)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<IDisplayService, WindowsDisplayService>();
            services.AddSingleton<IEdidReader, WindowsEdidReader>();
            services.AddSingleton<IScreenModeService, WindowsScreenModeService>();
            services.AddSingleton<IWallpaperService, WindowsWallpaperService>();
            services.AddSingleton<IWirelessDisplayLauncher, WindowsWirelessDisplayLauncher>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddSingleton<IDisplayService, MacDisplayService>();
            services.AddSingleton<IEdidReader, MacEdidReader>();
            services.AddSingleton<IScreenModeService, MacScreenModeService>();
            services.AddSingleton<IWallpaperService, MacWallpaperService>();
            services.AddSingleton<IWirelessDisplayLauncher, MacWirelessDisplayLauncher>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            services.AddSingleton<IDisplayService, LinuxDisplayService>();
            services.AddSingleton<IEdidReader, LinuxEdidReader>();
            services.AddSingleton<IScreenModeService, LinuxScreenModeService>();
            services.AddSingleton<IWallpaperService, LinuxWallpaperService>();
            services.AddSingleton<IWirelessDisplayLauncher, LinuxWirelessDisplayLauncher>();
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported operating system.");
        }

        // Settings Service
        services.AddSingleton<ISettingsService, SettingsService>();

        // Shared Setup State
        services.AddSingleton<IDisplaySetupState, DisplaySetupState>();

        // ViewModels
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<DisplaysViewModel>();
        services.AddSingleton<WirelessViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();

        return services;
    }
}
