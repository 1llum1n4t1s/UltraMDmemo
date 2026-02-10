namespace UltraMDmemo.Models;

public sealed class AppSettings
{
    public TransformIntent DefaultIntent { get; set; } = TransformIntent.Auto;
    public TransformMode DefaultMode { get; set; } = TransformMode.Balanced;
    public bool DefaultIncludeRaw { get; set; }
}
