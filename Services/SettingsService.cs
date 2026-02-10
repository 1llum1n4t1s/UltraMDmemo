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

    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_settingsPath))
            return new AppSettings();

        try
        {
            var json = await File.ReadAllTextAsync(_settingsPath, ct);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions.Default) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions.Default);
        await File.WriteAllTextAsync(_settingsPath, json, ct);
    }
}
