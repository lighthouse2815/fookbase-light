namespace Fookbase.Api.Shared.ErrorHandling;

public static class ErrorCode
{
    public const string ValidationFailed = "validation_failed";
    public const string InvalidRequest = "invalid_request";
    public const string InternalServerError = "internal_server_error";
    public const string Forbidden = "forbidden";
    public const string InvalidAccessToken = "invalid_access_token";
    public const string InvalidCredentials = "invalid_credentials";
    public const string InvalidPagination = "invalid_pagination";
}
