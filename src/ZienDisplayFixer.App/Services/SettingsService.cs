namespace ZienDisplayFixer.App.Services;

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ZienDisplayFixer.Core.Interfaces;
using ZienDisplayFixer.Core.Models;

public class SettingsService : ISettingsService, IDisposable
{
    private readonly string _settingsFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _lock = new(1, 1);
    private AppSettings _currentSettings = new();

    public AppSettings CurrentSettings => _currentSettings;

    public event EventHandler<AppSettings>? SettingsChanged;

    public SettingsService()
        : this(GetDefaultSettingsFilePath())
    {
    }

    public SettingsService(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
    }

    public static string GetDefaultSettingsFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "ZienDisplayFixer", "settings.json");
    }

    public async Task<AppSettings> LoadSettingsAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                _currentSettings = new AppSettings();
                return _currentSettings;
            }

            var json = await File.ReadAllTextAsync(_settingsFilePath, ct).ConfigureAwait(false);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            _currentSettings = loaded ?? new AppSettings();
            return _currentSettings;
        }
        catch
        {
            _currentSettings = new AppSettings();
            return _currentSettings;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await File.WriteAllTextAsync(_settingsFilePath, json, ct).ConfigureAwait(false);

            _currentSettings = settings;
        }
        finally
        {
            _lock.Release();
        }

        SettingsChanged?.Invoke(this, _currentSettings);
    }

    public void Dispose()
    {
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}

