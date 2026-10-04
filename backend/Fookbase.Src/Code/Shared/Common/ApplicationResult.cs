namespace Fookbase.Api.Shared.Common;

public enum ApplicationErrorType
{
    VALIDATION,
    UNAUTHORIZED,
    FORBIDDEN,
    NOT_FOUND,
    CONFLICT
}

public sealed record ApplicationError(
    string Code,
    string Message,
    ApplicationErrorType Type,
    IReadOnlyDictionary<string, string[]>? Details = null);

public sealed class ApplicationResult
{
    private ApplicationResult(bool succeeded, ApplicationError? error)
    {
        Succeeded = succeeded;
        Error = error;
    }

    public bool Succeeded { get; }

    public ApplicationError? Error { get; }

    public static ApplicationResult Success() => new(true, null);

    public static ApplicationResult Failure(ApplicationError error) => new(false, error);
}

public sealed class ApplicationResult<T>
{
    private ApplicationResult(bool succeeded, T? value, ApplicationError? error)
    {
        Succeeded = succeeded;
        Value = value;
        Error = error;
    }

    public bool Succeeded { get; }

    public T? Value { get; }

    public ApplicationError? Error { get; }

    public static ApplicationResult<T> Success(T value) => new(true, value, null);

    public static ApplicationResult<T> Failure(ApplicationError error) => new(false, default, error);
}
