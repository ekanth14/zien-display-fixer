namespace ZienDisplayFixer.App.Services.Linux;

using System;
using System.IO;
using ZienDisplayFixer.Core.Interfaces;

public class LinuxAutostartService : IAutostartService
{
    private static readonly string DefaultDesktopPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "autostart",
        "zien-display-fixer.desktop");

    private readonly string _desktopPath;

    public LinuxAutostartService()
        : this(DefaultDesktopPath)
    {
    }

    public LinuxAutostartService(string desktopPath)
    {
        _desktopPath = desktopPath;
    }

    public bool IsAutostartEnabled()
    {
        try
        {
            return File.Exists(_desktopPath);
        }
        catch
        {
            return false;
        }
    }

    public void SetAutostart(bool enable)
    {
        try
        {
            if (enable)
            {
                var directory = Path.GetDirectoryName(_desktopPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string? processPath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(processPath))
                {
                    processPath = AppDomain.CurrentDomain.BaseDirectory;
                }

                string desktopContent = $"""
[Desktop Entry]
Type=Application
Exec={processPath} --minimized
Hidden=false
NoDisplay=false
X-GNOME-Autostart-enabled=true
Name=ZIEN Display Fixer
Comment=Automated Display Resolution & Refresh Rate Fixer
""";
                File.WriteAllText(_desktopPath, desktopContent);
            }
            else
            {
                if (File.Exists(_desktopPath))
                {
                    File.Delete(_desktopPath);
                }
            }
        }
        catch
        {
            // Graceful fallback
        }
    }
}

