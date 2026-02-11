using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using UltraMDmemo.Models;
using UltraMDmemo.Services;
using UltraMDmemo.ViewModels;

namespace UltraMDmemo.Views;

public partial class MainWindow : Window
{
    private ISettingsService? _settingsService;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>View 側からウィンドウ状態を保存するために SettingsService を受け取る。</summary>
    public void SetSettingsService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.CopyRequested += OnCopyRequested;
            vm.SaveMarkdownRequested += OnSaveMarkdownRequested;
            vm.SaveMetaRequested += OnSaveMetaRequested;
            await vm.InitializeAsync();
        }
    }

    /// <summary>保存済み設定からウィンドウ位置・サイズを復元する。</summary>
    public void RestoreWindowState(AppSettings settings)
    {
        if (settings.IsMaximized)
        {
            WindowState = WindowState.Maximized;
            // 最大化前のサイズも保持しておく（復帰用）
            if (settings.WindowWidth is > 0)
                Width = settings.WindowWidth.Value;
            if (settings.WindowHeight is > 0)
                Height = settings.WindowHeight.Value;
        }
        else
        {
            if (settings.WindowWidth is > 0)
                Width = settings.WindowWidth.Value;
            if (settings.WindowHeight is > 0)
                Height = settings.WindowHeight.Value;
        }

        if (settings.WindowX is not null && settings.WindowY is not null)
        {
            // 保存済み座標がある場合は手動配置
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(settings.WindowX.Value, settings.WindowY.Value);
        }

        Logger.Log(
            $"ウィンドウ状態を復元: {Width}x{Height} at ({settings.WindowX},{settings.WindowY}), Maximized={settings.IsMaximized}",
            LogLevel.Debug);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        SaveWindowState();
    }

    /// <summary>ウィンドウ状態を同期的に保存する（OnClosing では async だとプロセス終了に間に合わない）。</summary>
    private void SaveWindowState()
    {
        if (_settingsService is null) return;

        try
        {
            var settings = _settingsService.Load();

            settings.IsMaximized = WindowState == WindowState.Maximized;

            // 最大化中は復帰用に直前のサイズを保持（最大化サイズは保存しない）
            if (WindowState != WindowState.Maximized)
            {
                settings.WindowWidth = ClientSize.Width;
                settings.WindowHeight = ClientSize.Height;
                settings.WindowX = Position.X;
                settings.WindowY = Position.Y;
            }

            _settingsService.Save(settings);
            Logger.Log(
                $"ウィンドウ状態を保存: {settings.WindowWidth}x{settings.WindowHeight} at ({settings.WindowX},{settings.WindowY}), Maximized={settings.IsMaximized}",
                LogLevel.Debug);
        }
        catch (Exception ex)
        {
            Logger.LogException("ウィンドウ状態の保存に失敗", ex);
        }
    }

    private async void OnCopyRequested(object? sender, string markdown)
    {
        var clipboard = GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(markdown);
            if (DataContext is MainWindowViewModel vm)
            {
                vm.StatusMessage = "クリップボードにコピーしました。";
            }
        }
    }

    private async void OnSaveMarkdownRequested(object? sender, string markdown)
    {
        var storageProvider = GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null) return;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Markdownファイルを保存",
            DefaultExtension = "md",
            FileTypeChoices =
            [
                new FilePickerFileType("Markdown") { Patterns = ["*.md"] },
                new FilePickerFileType("すべてのファイル") { Patterns = ["*.*"] },
            ],
            SuggestedFileName = "output.md",
        });

        if (file is not null)
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            await writer.WriteAsync(markdown);

            if (DataContext is MainWindowViewModel vm)
            {
                vm.StatusMessage = $"保存しました: {file.Name}";
            }
        }
    }

    private async void OnSaveMetaRequested(object? sender, TransformMeta meta)
    {
        var storageProvider = GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null) return;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Metaファイルを保存",
            DefaultExtension = "json",
            FileTypeChoices =
            [
                new FilePickerFileType("JSON") { Patterns = ["*.json"] },
            ],
            SuggestedFileName = $"{meta.Id}.meta.json",
        });

        if (file is not null)
        {
            await using var stream = await file.OpenWriteAsync();
            var json = JsonSerializer.Serialize(meta, AppJsonContext.Default.TransformMeta);
            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            await writer.WriteAsync(json);

            if (DataContext is MainWindowViewModel vm)
            {
                vm.StatusMessage = $"Meta保存しました: {file.Name}";
            }
        }
    }
}
