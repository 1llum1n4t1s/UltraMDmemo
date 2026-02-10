using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using UltraMDmemo.Models;
using UltraMDmemo.ViewModels;

namespace UltraMDmemo.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
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
            var json = JsonSerializer.Serialize(meta, JsonOptions.Default);
            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            await writer.WriteAsync(json);

            if (DataContext is MainWindowViewModel vm)
            {
                vm.StatusMessage = $"Meta保存しました: {file.Name}";
            }
        }
    }
}
