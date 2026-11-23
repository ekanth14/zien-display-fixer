namespace ZienDisplayFixer.Tests.Services;

using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using ZienDisplayFixer.App.Services;
using ZienDisplayFixer.Core.Models;

public class SettingsServiceTests : IDisposable
{
    private readonly string _testDirectory;

    public SettingsServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "ZienSettingsTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup failures in temp dir
        }
    }

    [Fact]
    public void DefaultPath_PointsToApplicationDataFolder()
    {
        var defaultPath = SettingsService.GetDefaultSettingsFilePath();
        Assert.NotNull(defaultPath);
        Assert.Contains("ZienDisplayFixer", defaultPath);
        Assert.EndsWith("settings.json", defaultPath);
    }

    [Fact]
    public void DefaultValues_WhenCreated_MatchesExpectedDefaults()
    {
        var filePath = Path.Combine(_testDirectory, "settings.json");
        var service = new SettingsService(filePath);

        Assert.NotNull(service.CurrentSettings);
        Assert.True(service.CurrentSettings.FixAutomaticallyOnConnect);
        Assert.False(service.CurrentSettings.StartAtLogin);
        Assert.True(service.CurrentSettings.IsAnimationEnabled);
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenFileDoesNotExist_ReturnsDefaultSettings()
    {
        var filePath = Path.Combine(_testDirectory, "non_existent.json");
        var service = new SettingsService(filePath);

        var loaded = await service.LoadSettingsAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(loaded);
        Assert.True(loaded.FixAutomaticallyOnConnect);
        Assert.False(loaded.StartAtLogin);
        Assert.True(loaded.IsAnimationEnabled);
        Assert.Same(loaded, service.CurrentSettings);
    }

    [Fact]
    public async Task SaveAndLoad_Roundtrip_PreservesAllSettingsAndFiresEvent()
    {
        var filePath = Path.Combine(_testDirectory, "roundtrip.json");
        var service = new SettingsService(filePath);

        AppSettings? eventPayload = null;
        service.SettingsChanged += (_, s) => eventPayload = s;

        var modified = new AppSettings
        {
            FixAutomaticallyOnConnect = false,
            StartAtLogin = true,
            IsAnimationEnabled = false
        };

        await service.SaveSettingsAsync(modified, TestContext.Current.CancellationToken);

        Assert.NotNull(eventPayload);
        Assert.False(eventPayload.FixAutomaticallyOnConnect);
        Assert.True(eventPayload.StartAtLogin);
        Assert.False(eventPayload.IsAnimationEnabled);

        // Load with a new service instance pointing to the same file
        var secondService = new SettingsService(filePath);
        var loaded = await secondService.LoadSettingsAsync(TestContext.Current.CancellationToken);

        Assert.False(loaded.FixAutomaticallyOnConnect);
        Assert.True(loaded.StartAtLogin);
        Assert.False(loaded.IsAnimationEnabled);
    }

    [Fact]
    public async Task LoadSettingsAsync_CorruptedJson_RecoversWithDefaultSettings()
    {
        var filePath = Path.Combine(_testDirectory, "corrupted.json");
        await File.WriteAllTextAsync(filePath, "{ invalid json text content <<<", TestContext.Current.CancellationToken);

        var service = new SettingsService(filePath);
        var loaded = await service.LoadSettingsAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(loaded);
        Assert.True(loaded.FixAutomaticallyOnConnect);
        Assert.False(loaded.StartAtLogin);
        Assert.True(loaded.IsAnimationEnabled);
    }

    [Fact]
    public async Task SaveSettingsAsync_CustomNestedDirectory_CreatesDirectoryAndPersistsFile()
    {
        var nestedPath = Path.Combine(_testDirectory, "A", "B", "C", "custom_settings.json");
        var service = new SettingsService(nestedPath);

        var settings = new AppSettings
        {
            FixAutomaticallyOnConnect = true,
            StartAtLogin = true,
            IsAnimationEnabled = false
        };

        await service.SaveSettingsAsync(settings, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(nestedPath));
        var content = await File.ReadAllTextAsync(nestedPath, TestContext.Current.CancellationToken);
        Assert.Contains("\"StartAtLogin\": true", content);
        Assert.Contains("\"IsAnimationEnabled\": false", content);
    }
}
