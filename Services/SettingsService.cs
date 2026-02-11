using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UltraMDmemo.Models;

namespace UltraMDmemo.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly string _settingsPath;

    public SettingsService()
    {
        AppPaths.EnsureDirectories();
        _settingsPath = AppPaths.SettingsPath;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Logger.LogException("設定ファイルの読み込みに失敗（デフォルトにフォールバック）", ex);
            return new AppSettings();
        }
    }

    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_settingsPath))
            return new AppSettings();

        try
        {
            var json = await File.ReadAllTextAsync(_settingsPath, ct);
            return JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Logger.LogException("設定ファイルの非同期読み込みに失敗（デフォルトにフォールバック）", ex);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, AppJsonContext.Default.AppSettings);
        var tempPath = _settingsPath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _settingsPath, overwrite: true);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(settings, AppJsonContext.Default.AppSettings);
        var tempPath = _settingsPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, ct);
        File.Move(tempPath, _settingsPath, overwrite: true);
    }
}
