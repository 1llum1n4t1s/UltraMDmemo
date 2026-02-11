using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using UltraMDmemo.Services;
using Velopack;
using Velopack.Sources;

namespace UltraMDmemo;

[SupportedOSPlatform("windows")]
internal sealed class Program
{
    /// <summary>
    /// 更新チェックのタイムアウト時間（ミリ秒）
    /// </summary>
    private const int UpdateCheckTimeoutMs = 10_000;

    /// <summary>
    /// 更新リポジトリのオーナー
    /// </summary>
    private const string RepoOwner = "szk-oss";

    /// <summary>
    /// 更新リポジトリ名
    /// </summary>
    private const string RepoName = "UltraMDmemo";

    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack 初期化（インストーラーのフック処理）
        VelopackApp.Build()
            .OnAfterInstallFastCallback(_ => StartupRegistration.Register())
            .OnAfterUpdateFastCallback(_ => StartupRegistration.Register())
            .OnBeforeUninstallFastCallback(_ => StartupRegistration.Unregister())
            .Run();

        // サイレント更新チェックモード（Windows ログイン時のスタートアップから呼び出される）
        if (args.Length > 0 && args[0] == "--update-check")
        {
            RunSilentUpdateCheck();
            return;
        }

        // ログ初期化
        Logger.Initialize(new LoggerConfig
        {
            LogDirectory = AppPaths.LogDirectory,
            FilePrefix = "UltraMDmemo",
        });
        Logger.LogStartup(args);

        // プロセス起動からの経過を記録
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Logger.Log("[Startup] Avalonia ビルド開始", LogLevel.Info);

        // 通常の Avalonia 起動シーケンスへ
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        Logger.Log($"[Startup] アプリケーション終了 ({sw.ElapsedMilliseconds}ms)", LogLevel.Info);
        Logger.Dispose();
    }

    /// <summary>
    /// UI なしでサイレント更新チェックを実行する。
    /// Windows ログイン時のスタートアップから呼び出される。
    /// </summary>
    private static void RunSilentUpdateCheck()
    {
        try
        {
            var repoUrl = $"https://github.com/{RepoOwner}/{RepoName}";
            var source = new GithubSource(repoUrl, string.Empty, false);
            var updateManager = new UpdateManager(source);

            if (!updateManager.IsInstalled)
            {
                Console.Error.WriteLine("開発実行のため更新チェックをスキップします。");
                return;
            }

            Console.Error.WriteLine($"サイレント更新チェック: リポジトリ: {repoUrl}");

            // 更新チェック（タイムアウト付き）
            UpdateInfo? updateInfo;
            try
            {
                var checkTask = updateManager.CheckForUpdatesAsync();
                var timeoutTask = Task.Delay(UpdateCheckTimeoutMs);
                if (Task.WhenAny(checkTask, timeoutTask).GetAwaiter().GetResult() == timeoutTask)
                {
                    Console.Error.WriteLine("更新チェックがタイムアウトしました。");
                    return;
                }
                updateInfo = checkTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("更新チェックがタイムアウトしました。");
                return;
            }

            if (updateInfo == null)
            {
                Console.Error.WriteLine("利用可能な更新はありません。");
                return;
            }

            Console.Error.WriteLine("新しいバージョンを検出しました。更新をダウンロードしています...");

            // ダウンロード（10分タイムアウト）
            try
            {
                using var downloadCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                updateManager.DownloadUpdatesAsync(updateInfo, null, downloadCts.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("ダウンロードがタイムアウトしました。");
                return;
            }

            Console.Error.WriteLine("ダウンロード完了。更新を適用します。");
            updateManager.ApplyUpdatesAndExit(updateInfo);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"サイレント更新チェック中にエラーが発生しました: {ex.Message}");
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
