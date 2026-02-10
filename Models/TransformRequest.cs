namespace UltraMDmemo.Models;

public sealed class TransformRequest
{
    public required string Text { get; init; }
    public TransformIntent Intent { get; init; } = TransformIntent.Auto;
    public TransformMode Mode { get; init; } = TransformMode.Balanced;
    public bool IncludeRaw { get; init; } = true;
    public string? TitleHint { get; init; }
}
