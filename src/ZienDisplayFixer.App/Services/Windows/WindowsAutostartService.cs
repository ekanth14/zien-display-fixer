namespace ZienDisplayFixer.App.Services.Windows;

using System;
using System.IO;
using Microsoft.Win32;
using ZienDisplayFixer.Core.Interfaces;

public class WindowsAutostartService : IAutostartService
{
    private const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppKeyName = "ZienDisplayFixer";

    private readonly string _runKeyPath;

    public WindowsAutostartService(string runKeyPath = DefaultRunKeyPath)
    {
        _runKeyPath = runKeyPath;
    }

    public bool IsAutostartEnabled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath, false);
            var value = key?.GetValue(AppKeyName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    public void SetAutostart(bool enable)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            if (enable)
            {
                using var key = Registry.CurrentUser.CreateSubKey(_runKeyPath, true);
                if (key == null) return;

                string? processPath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(processPath))
                {
                    processPath = AppDomain.CurrentDomain.BaseDirectory;
                }

                string command = $"\"{processPath}\" --minimized";
                key.SetValue(AppKeyName, command);
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath, true);
                if (key != null && key.GetValue(AppKeyName) != null)
                {
                    key.DeleteValue(AppKeyName, false);
                }
            }
        }
        catch
        {
            // Graceful fallback
        }
    }
}
