namespace Fookbase.Api.Shared.ErrorHandling;

public sealed record ErrorCode(string Code, string Message)
{
    public static readonly ErrorCode ValidationFailed = new(
        "validation_failed",
        "One or more validation errors occurred.");
    public static readonly ErrorCode InvalidRequest = new(
        "invalid_request",
        "The request body is invalid.");
    public static readonly ErrorCode InternalServerError = new(
        "internal_server_error",
        "An unexpected error occurred.");
    public static readonly ErrorCode Forbidden = new(
        "forbidden",
        "You are not allowed to perform this operation.");
    public static readonly ErrorCode InvalidAccessToken = new(
        "invalid_access_token",
        "The access token is invalid.");
    public static readonly ErrorCode InvalidCredentials = new(
        "invalid_credentials",
        "The credentials are invalid.");
    public static readonly ErrorCode InvalidPagination = new(
        "invalid_pagination",
        "The pagination parameters are invalid.");

    public static implicit operator string(ErrorCode errorCode) => errorCode.Code;
}
