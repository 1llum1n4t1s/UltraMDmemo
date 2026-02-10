namespace UltraMDmemo.Models;

public enum ErrorCode
{
    InputTooLarge,
    CliFailed,
    Timeout,
    InvalidRequest,
    SetupFailed,
    LoginRequired,
    LoginTimeout
}

public sealed class TransformError
{
    public required ErrorCode Code { get; init; }
    public required string Message { get; init; }
    public string? Details { get; init; }
}
