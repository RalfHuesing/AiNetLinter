#nullable enable

namespace AiNetLinter.Mcp.Handoffs;

internal readonly record struct ResultError(string Code, string Message, string? Hint = null);

internal readonly record struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public ResultError? Error { get; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
        Error = null;
    }

    private Result(ResultError error)
    {
        IsSuccess = false;
        Value = default;
        Error = error;
    }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(string code, string message, string? hint = null) =>
        new(new ResultError(code, message, hint));

    public static Result<T> Failure(ResultError error) => new(error);
    public static Result<T> Failure(ResultError? error) =>
        new(error ?? new ResultError("ERROR", "Unbekannter Fehler."));

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(ResultError error) => Failure(error);
}
