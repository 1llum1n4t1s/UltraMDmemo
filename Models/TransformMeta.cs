using System;
using System.Collections.Generic;

namespace UltraMDmemo.Models;

public sealed class TransformMeta
{
    public required string Id { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string Title { get; init; }
    public required string Intent { get; init; }
    public required string Mode { get; init; }
    public required bool IncludeRaw { get; init; }
    public string? TitleHint { get; init; }
    public required int InputChars { get; init; }
    public required long DurationMs { get; init; }
    public List<string> Warnings { get; init; } = [];
    public required HistoryPaths Paths { get; init; }
}
