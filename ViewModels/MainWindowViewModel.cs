using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltraMDmemo.Models;
using UltraMDmemo.Services;

namespace UltraMDmemo.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ITransformService _transformService;
    private readonly IHistoryService _historyService;
    private readonly ISettingsService _settingsService;
    private readonly IClaudeCodeSetupService _setupService;
    private CancellationTokenSource? _currentCts;
    private TransformResult? _lastResult;

    // ---- Initialization State ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMainVisible))]
    private bool _isInitializing = true;

    [ObservableProperty]
    private string _initializationStep = string.Empty;

    public bool IsMainVisible => !IsInitializing;

    // ---- Input State ----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TransformCommand))]
    [NotifyPropertyChangedFor(nameof(InputLength))]
    [NotifyPropertyChangedFor(nameof(IsInputTooLong))]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private string _titleHint = string.Empty;

    [ObservableProperty]
    private LabeledValue<TransformIntent> _selectedIntentItem;

    [ObservableProperty]
    private LabeledValue<TransformMode> _selectedModeItem;

    [ObservableProperty]
    private bool _includeRaw;

    // ---- Output State ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutput))]
    private string _outputMarkdown = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TransformCommand))]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private long _durationMs;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TransformCommand))]
    private bool _isCliAvailable;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private int _selectedOutputTab;

    // ---- Computed Properties ----

    public int InputLength => InputText.Length;
    public bool IsInputTooLong => InputText.Length > 20_000;
    public bool HasOutput => !string.IsNullOrEmpty(OutputMarkdown);

    // ---- Dropdown Sources ----

    public LabeledValue<TransformIntent>[] IntentOptions { get; } =
    [
        new(TransformIntent.Auto, "自動判定"),
        new(TransformIntent.Meeting, "会議メモ"),
        new(TransformIntent.Requirements, "要件メモ"),
        new(TransformIntent.Incident, "インシデント記録"),
        new(TransformIntent.Study, "学習ノート"),
        new(TransformIntent.DraftArticle, "記事下書き"),
        new(TransformIntent.ChatSummary, "チャット要約"),
        new(TransformIntent.Generic, "汎用メモ"),
    ];

    public LabeledValue<TransformMode>[] ModeOptions { get; } =
    [
        new(TransformMode.Balanced, "バランス"),
        new(TransformMode.Strict, "厳密"),
        new(TransformMode.Compact, "簡潔"),
        new(TransformMode.Verbose, "詳細"),
    ];

    // ---- History ----

    [ObservableProperty]
    private ObservableCollection<TransformMeta> _historyItems = [];

    [ObservableProperty]
    private TransformMeta? _selectedHistoryItem;

    [ObservableProperty]
    private bool _isHistoryPanelVisible;

    // ---- Events for View code-behind ----

    public event EventHandler<string>? CopyRequested;
    public event EventHandler<string>? SaveMarkdownRequested;
    public event EventHandler<TransformMeta>? SaveMetaRequested;

    // ---- Constructor ----

    public MainWindowViewModel(
        ITransformService transformService,
        IHistoryService historyService,
        ISettingsService settingsService,
        IClaudeCodeSetupService setupService)
    {
        _transformService = transformService;
        _historyService = historyService;
        _settingsService = settingsService;
        _setupService = setupService;

        _selectedIntentItem = IntentOptions[0];
        _selectedModeItem = ModeOptions[0];
    }

    // ---- Initialization (起動シーケンス: Section 5, 8.2) ----

    public async Task InitializeAsync()
    {
        IsInitializing = true;
        var progress = new Progress<string>(msg => InitializationStep = msg);

        try
        {
            // Step 1: 設定ロード + 履歴読込を並列開始
            InitializationStep = "設定を読み込み中...";
            var settingsTask = _settingsService.LoadAsync();
            var historyTask = _historyService.LoadIndexAsync();

            var settings = await settingsTask;
            SelectedIntentItem = IntentOptions.FirstOrDefault(x => x.Value == settings.DefaultIntent) ?? IntentOptions[0];
            SelectedModeItem = ModeOptions.FirstOrDefault(x => x.Value == settings.DefaultMode) ?? ModeOptions[0];
            IncludeRaw = settings.DefaultIncludeRaw;

            // Step 2-3: Node.js / CLI の存在確認（インストール済みならファイル存在チェックのみ）
            InitializationStep = "環境を確認中...";
            await _setupService.EnsureNodeJsAsync(progress);
            await _setupService.EnsureCliAsync(progress);

            // Step 4: ログイン状態チェック（= 接続検証を兼ねる）
            InitializationStep = "接続を確認中...";
            var isLoggedIn = await _setupService.IsLoggedInAsync();

            // Step 5: 未ログインの場合 → ブラウザ認証起動 + ポーリング
            if (!isLoggedIn)
            {
                InitializationStep = "ブラウザで認証を開始します...";
                await _setupService.RunLoginAsync(progress);
                // ログイン後に再度確認
                isLoggedIn = await _setupService.IsLoggedInAsync();
            }

            IsCliAvailable = isLoggedIn;

            if (!isLoggedIn)
            {
                ErrorMessage = "Claude Code との接続が確認できません。再起動して再試行してください。";
            }

            // Step 6: 履歴読込の完了を待つ（Step 1 で並列開始済み）
            var historyItems = await historyTask;
            HistoryItems = new ObservableCollection<TransformMeta>(historyItems);

            InitializationStep = "準備完了";
        }
        catch (TimeoutException ex)
        {
            ErrorMessage = $"ログインタイムアウト: {ex.Message}";
            InitializationStep = "認証がタイムアウトしました";
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = $"セットアップエラー: {ex.Message}";
            InitializationStep = "セットアップに失敗しました";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"初期化エラー: {ex.Message}";
            InitializationStep = "初期化に失敗しました";
        }
        finally
        {
            IsInitializing = false;
        }
    }

    // ---- Commands ----

    private bool CanTransform() =>
        !IsProcessing
        && !string.IsNullOrWhiteSpace(InputText)
        && InputText.Length <= 20_000
        && IsCliAvailable;

    [RelayCommand(CanExecute = nameof(CanTransform))]
    private async Task TransformAsync()
    {
        IsProcessing = true;
        ErrorMessage = string.Empty;
        StatusMessage = "整形中...";
        _currentCts = new CancellationTokenSource();

        try
        {
            var request = new TransformRequest
            {
                Text = InputText,
                Intent = SelectedIntentItem.Value,
                Mode = SelectedModeItem.Value,
                IncludeRaw = IncludeRaw,
                TitleHint = string.IsNullOrWhiteSpace(TitleHint) ? null : TitleHint,
            };

            var result = await _transformService.TransformAsync(request, _currentCts.Token);
            _lastResult = result;
            OutputMarkdown = result.Markdown;
            DurationMs = result.Meta.DurationMs;

            var warningText = result.Meta.Warnings.Count > 0
                ? $" | 警告: {string.Join(", ", result.Meta.Warnings)}"
                : "";
            StatusMessage = $"完了 ({result.Meta.DurationMs}ms, {result.Meta.InputChars}文字){warningText}";

            await ReloadHistoryAsync();
        }
        catch (TimeoutException)
        {
            ErrorMessage = "タイムアウト: Claude CLI が120秒以内に応答しませんでした。";
            StatusMessage = "エラー: タイムアウト";
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = $"CLI エラー: {ex.Message}";
            StatusMessage = "エラー: CLI実行失敗";
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "エラー: 入力不正";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "キャンセルされました。";
        }
        finally
        {
            IsProcessing = false;
            _currentCts?.Dispose();
            _currentCts = null;
        }
    }

    [RelayCommand]
    private void Clear()
    {
        InputText = string.Empty;
        OutputMarkdown = string.Empty;
        TitleHint = string.Empty;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        DurationMs = 0;
        _lastResult = null;
    }

    [RelayCommand]
    private void RequestCopy()
    {
        CopyRequested?.Invoke(this, OutputMarkdown);
    }

    [RelayCommand]
    private void RequestSaveMarkdown()
    {
        SaveMarkdownRequested?.Invoke(this, OutputMarkdown);
    }

    [RelayCommand]
    private void RequestSaveMeta()
    {
        if (_lastResult?.Meta is not null)
        {
            SaveMetaRequested?.Invoke(this, _lastResult.Meta);
        }
    }

    // ---- History Commands ----

    [RelayCommand]
    private void ToggleHistory()
    {
        IsHistoryPanelVisible = !IsHistoryPanelVisible;
    }

    [RelayCommand]
    private async Task ReloadHistoryAsync()
    {
        var items = await _historyService.LoadIndexAsync();
        HistoryItems = new ObservableCollection<TransformMeta>(items);
    }

    [RelayCommand]
    private async Task LoadHistoryItemAsync(TransformMeta? meta)
    {
        if (meta is null) return;
        try
        {
            var (input, output, _) = await _historyService.LoadAsync(meta.Id);
            InputText = input;
            OutputMarkdown = output;
            _lastResult = new TransformResult { Markdown = output, Meta = meta };
            StatusMessage = $"履歴から読込: {meta.Title}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"履歴の読み込みに失敗: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteHistoryItemAsync(TransformMeta? meta)
    {
        if (meta is null) return;
        try
        {
            await _historyService.DeleteAsync(meta.Id);
            await ReloadHistoryAsync();
            StatusMessage = $"履歴を削除: {meta.Title}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"履歴の削除に失敗: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RerunHistoryItemAsync(TransformMeta? meta)
    {
        if (meta is null) return;
        try
        {
            var (input, _, _) = await _historyService.LoadAsync(meta.Id);
            InputText = input;
            if (CanTransform())
            {
                await TransformAsync();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"再整形に失敗: {ex.Message}";
        }
    }
}
