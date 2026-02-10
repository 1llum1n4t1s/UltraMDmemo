using System;
using Avalonia;
using Velopack;

namespace UltraMDmemo;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack 初期化（インストーラーのフック処理）
        VelopackApp.Build().Run();

        // 自動更新チェック
        try
        {
            var mgr = new UpdateManager("https://github.com/szk-oss/UltraMDmemo/releases/latest/download");
            var updateInfo = mgr.CheckForUpdates();
            if (updateInfo is not null)
            {
                mgr.DownloadUpdates(updateInfo);
                mgr.ApplyUpdatesAndRestart(updateInfo);
                return; // 再起動するので終了
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Velopack 未インストール環境（開発環境）や更新チェック失敗時は
            // ログ出力のみで起動を継続（更新失敗でアプリが使えなくなることを防止）
            Console.Error.WriteLine($"自動更新チェックをスキップ: {ex.Message}");
        }

        // 通常の Avalonia 起動シーケンスへ
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
