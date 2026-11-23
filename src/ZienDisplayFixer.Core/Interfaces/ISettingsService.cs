namespace ZienDisplayFixer.Core.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;
using ZienDisplayFixer.Core.Models;

public interface ISettingsService
{
    AppSettings CurrentSettings { get; }
    Task<AppSettings> LoadSettingsAsync(CancellationToken ct = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default);
    event EventHandler<AppSettings>? SettingsChanged;
}

