using System.Text.Json.Serialization;

namespace UltraMDmemo.Models;

/// <summary>
/// JSON Source Generator 用のコンテキストクラス。
/// Native AOT 環境での JSON シリアライズをサポートします。
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(TransformMeta))]
[JsonSerializable(typeof(AppSettings))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
