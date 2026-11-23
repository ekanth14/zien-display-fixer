namespace ZienDisplayFixer.App.Services;

using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using ZienDisplayFixer.App.Services.Linux;
using ZienDisplayFixer.App.Services.MacOS;
using ZienDisplayFixer.App.Services.Windows;
using ZienDisplayFixer.Core.Interfaces;

public static class ServiceRegistration
{
    public static IServiceCollection AddDisplayFixerServices(this IServiceCollection services)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<IDisplayService, WindowsDisplayService>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddSingleton<IDisplayService, MacDisplayService>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            services.AddSingleton<IDisplayService, LinuxDisplayService>();
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported operating system.");
        }

        return services;
    }
}
