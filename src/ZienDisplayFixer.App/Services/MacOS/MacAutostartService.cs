namespace ZienDisplayFixer.App.Services.MacOS;

using System;
using System.IO;
using ZienDisplayFixer.Core.Interfaces;

public class MacAutostartService : IAutostartService
{
    private static readonly string DefaultPlistPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library",
        "LaunchAgents",
        "com.zien.displayfixer.plist");

    private readonly string _plistPath;

    public MacAutostartService()
        : this(DefaultPlistPath)
    {
    }

    public MacAutostartService(string plistPath)
    {
        _plistPath = plistPath;
    }

    public bool IsAutostartEnabled()
    {
        try
        {
            return File.Exists(_plistPath);
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
                var directory = Path.GetDirectoryName(_plistPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string? processPath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(processPath))
                {
                    processPath = AppDomain.CurrentDomain.BaseDirectory;
                }

                string plistContent = $"""
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.zien.displayfixer</string>
    <key>ProgramArguments</key>
    <array>
        <string>{processPath}</string>
        <string>--minimized</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
</dict>
</plist>
""";
                File.WriteAllText(_plistPath, plistContent);
            }
            else
            {
                if (File.Exists(_plistPath))
                {
                    File.Delete(_plistPath);
                }
            }
        }
        catch
        {
            // Graceful fallback
        }
    }
}

