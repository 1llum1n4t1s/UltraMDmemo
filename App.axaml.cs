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
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            var setupService = new ClaudeCodeSetupService();
            var processHost = new ClaudeCodeProcessHost(setupService);
            var historyService = new HistoryService();
            var transformService = new TransformService(processHost, historyService);
            var settingsService = new SettingsService();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    transformService, historyService, settingsService, setupService),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
