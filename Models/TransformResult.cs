namespace UltraMDmemo.Models;

public sealed class TransformResult
{
    public required string Markdown { get; init; }
    public required TransformMeta Meta { get; init; }
}
