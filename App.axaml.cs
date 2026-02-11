using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using UltraMDmemo.Services;
using UltraMDmemo.ViewModels;
using UltraMDmemo.Views;

namespace UltraMDmemo;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var sw = Stopwatch.StartNew();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            var setupService = new ClaudeCodeSetupService();
            var processHost = new ClaudeCodeProcessHost(setupService);
            var historyService = new HistoryService();
            var transformService = new TransformService(processHost, historyService);
            var settingsService = new SettingsService();
            Logger.Log($"[Startup] サービス生成完了 ({sw.ElapsedMilliseconds}ms)", LogLevel.Info);

            // 設定を同期読み込み（async void で base 呼び出しが遅延するのを防ぐ）
            var settings = settingsService.Load();
            Logger.Log($"[Startup] 設定ファイル読み込み完了 ({sw.ElapsedMilliseconds}ms)", LogLevel.Info);

            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    transformService, historyService, settingsService, setupService),
            };

            mainWindow.SetSettingsService(settingsService);
            mainWindow.RestoreWindowState(settings);

            desktop.MainWindow = mainWindow;
            Logger.Log($"[Startup] MainWindow 生成・復元完了 ({sw.ElapsedMilliseconds}ms)", LogLevel.Info);
        }

        base.OnFrameworkInitializationCompleted();
        Logger.Log($"[Startup] OnFrameworkInitializationCompleted 完了 ({sw.ElapsedMilliseconds}ms)", LogLevel.Info);
    }

    // Avalonia テンプレート由来のコード。DataValidators はトリミング非対応だが実行時に問題なし。
#pragma warning disable IL2026
    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
#pragma warning restore IL2026
}
