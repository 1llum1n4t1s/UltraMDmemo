namespace UltraMDmemo.Models;

/// <summary>
/// ドロップダウン表示用のラベル付き値。
/// ToString() で日本語ラベルを返し、ComboBox の表示に使う。
/// </summary>
public sealed class LabeledValue<T>(T value, string label)
{
    public T Value { get; } = value;
    public string Label { get; } = label;

    public override string ToString() => Label;
}
