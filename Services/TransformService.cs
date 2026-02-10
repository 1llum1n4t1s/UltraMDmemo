using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UltraMDmemo.Models;

namespace UltraMDmemo.Services;

public sealed class TransformService : ITransformService
{
    private const int MaxInputLength = 20_000;
    private static readonly Regex InvalidFileChars = new(@"[\\/:*?""<>|]", RegexOptions.Compiled);

    private readonly IClaudeCodeProcessHost _processHost;
    private readonly IHistoryService _historyService;

    public TransformService(IClaudeCodeProcessHost processHost, IHistoryService historyService)
    {
        _processHost = processHost;
        _historyService = historyService;
    }

    public async Task<TransformResult> TransformAsync(TransformRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new ArgumentException("入力テキストが空です。");
        if (request.Text.Length > MaxInputLength)
            throw new ArgumentException($"入力が {MaxInputLength} 文字を超えています。");

        var prompt = BuildPrompt(request);
        var sw = Stopwatch.StartNew();
        var markdown = await _processHost.ExecuteAsync(prompt, request.Text, ct);
        sw.Stop();

        var warnings = ValidateOutput(markdown);
        var id = GenerateId();
        var now = DateTimeOffset.Now;
        var title = ExtractTitle(markdown, now);

        var paths = _historyService.GetPaths(id);
        var meta = new TransformMeta
        {
            Id = id,
            CreatedAt = now,
            Title = title,
            Intent = request.Intent.ToString().ToLowerInvariant(),
            Mode = request.Mode.ToString().ToLowerInvariant(),
            IncludeRaw = request.IncludeRaw,
            TitleHint = request.TitleHint,
            InputChars = request.Text.Length,
            DurationMs = sw.ElapsedMilliseconds,
            Warnings = warnings,
            Paths = paths,
        };

        await _historyService.SaveAsync(id, request.Text, markdown, meta, ct);

        return new TransformResult { Markdown = markdown, Meta = meta };
    }

    private static string BuildPrompt(TransformRequest request)
    {
        var intentLabel = request.Intent switch
        {
            TransformIntent.Meeting => "会議メモ",
            TransformIntent.Requirements => "要件メモ",
            TransformIntent.Incident => "インシデント記録",
            TransformIntent.Study => "学習ノート",
            TransformIntent.DraftArticle => "記事下書き",
            TransformIntent.ChatSummary => "チャット要約",
            TransformIntent.Generic => "汎用メモ",
            _ => "自動判定"
        };

        var modeInstruction = request.Mode switch
        {
            TransformMode.Strict => "厳密に原文に忠実に整理してください。",
            TransformMode.Compact => "簡潔にまとめてください。",
            TransformMode.Verbose => "詳細に展開してください。",
            _ => "バランスよく整理してください。"
        };

        var rawSection = request.IncludeRaw
            ? "最後に「---」の後に「## 原文」セクションとして入力テキストをそのまま含めてください。"
            : "原文セクションは不要です。";

        var titleHint = !string.IsNullOrWhiteSpace(request.TitleHint)
            ? $"タイトルのヒント: {request.TitleHint}"
            : "";

        return $"""
            あなたはメモ整形アシスタントです。入力されたテキストを以下のMarkdown構造に変換してください。

            ## 出力テンプレート（必須）:
            1. # タイトル（形式: YYYY-MM-DD HH:mm_推定タイトル、推定タイトルは最大30文字）
            2. ## サマリー
            3. ## 要点
            4. ## 詳細
            5. ## 不明点 / 要確認

            ## 任意セクション（内容がある場合のみ）:
            - ## 決定事項 / 結論
            - ## TODO / 次アクション（TODOは - [ ] 形式）
            - ## 参照 / リンク

            {rawSection}

            ## ルール:
            - 推測で情報を補完しない（不明な点は「不明点」に記載）
            - 固有名詞・専門用語は原文のまま維持
            - 文書の種類: {intentLabel}
            - {modeInstruction}
            {titleHint}

            入力テキストが標準入力から渡されます。Markdownのみを出力してください。説明や前置きは不要です。
            """;
    }

    private static List<string> ValidateOutput(string markdown)
    {
        var warnings = new List<string>();
        ReadOnlySpan<string> requiredSections = ["# ", "## サマリー", "## 要点", "## 詳細", "## 不明点"];

        foreach (var section in requiredSections)
        {
            if (!markdown.Contains(section, StringComparison.Ordinal))
            {
                warnings.Add($"必須セクションが見つかりません: {section.TrimStart('#', ' ')}");
            }
        }

        return warnings;
    }

    private static string GenerateId()
    {
        var now = DateTime.Now;
        var rand = Random.Shared.Next(0, 0xFFFFFF).ToString("x6");
        return $"{now:yyyyMMdd_HHmmss}_{rand}";
    }

    private static string ExtractTitle(string markdown, DateTimeOffset now)
    {
        var lines = markdown.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("# ") && !trimmed.StartsWith("## "))
            {
                var title = trimmed[2..].Trim();
                if (!string.IsNullOrWhiteSpace(title))
                {
                    return InvalidFileChars.Replace(title, "");
                }
            }
        }

        return $"{now:yyyy-MM-dd HH:mm}_無題のメモ";
    }
}
