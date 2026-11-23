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
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddSingleton<IDisplayService, MacDisplayService>();
            services.AddSingleton<IEdidReader, MacEdidReader>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            services.AddSingleton<IDisplayService, LinuxDisplayService>();
            services.AddSingleton<IEdidReader, LinuxEdidReader>();
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported operating system.");
        }

        // ViewModels
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<DisplaysViewModel>();
        services.AddSingleton<WirelessViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();

        return services;
    }
}
