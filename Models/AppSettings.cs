using System.Text.Json.Serialization;

namespace UltraMDmemo.Models;

public sealed class AppSettings
{
    // --- 変換オプション ---
    [JsonConverter(typeof(JsonStringEnumConverter<TransformIntent>))]
    public TransformIntent DefaultIntent { get; set; } = TransformIntent.Auto;

    [JsonConverter(typeof(JsonStringEnumConverter<TransformMode>))]
    public TransformMode DefaultMode { get; set; } = TransformMode.Balanced;
    public bool DefaultIncludeRaw { get; set; }
    public string? DefaultTitleHint { get; set; }

    // --- ウィンドウ状態 ---
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public bool IsMaximized { get; set; }
}
