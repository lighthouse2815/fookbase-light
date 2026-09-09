namespace Fookbase.Api.Modules.Users.Common;

public enum ApplicationErrorType
{
    Validation,
    NotFound
}

public sealed record ApplicationError(
    string Code,
    string Message,
    ApplicationErrorType Type,
    IReadOnlyDictionary<string, string[]>? Details = null);

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

    public static ApplicationResult<T> Failure(ApplicationError error) =>
        new(false, default, error);
}
