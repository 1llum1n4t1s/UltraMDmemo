using System.Threading;
using System.Threading.Tasks;
using UltraMDmemo.Models;

namespace UltraMDmemo.Services;

public interface ISettingsService
{
    /// <summary>設定を同期的に読み込む（起動時のウィンドウ復元など await 不可の場面用）。</summary>
    AppSettings Load();
    Task<AppSettings> LoadAsync(CancellationToken ct = default);

    /// <summary>設定を同期的に保存する（OnClosing など await 不可の場面用）。</summary>
    void Save(AppSettings settings);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
