using Fookbase.Api.Shared.Observability;
using Microsoft.AspNetCore.WebUtilities;

namespace Fookbase.Api.Shared.Common;

public sealed record ApiResponse<T>(
    bool Success,
    T? Data,
    ApiResponseError? Error,
    string RequestId
);

public sealed record ApiResponseError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Details = null
);

public static class ApiResponse
{
    public static bool AppliesTo(HttpContext context) => context.Request.Path.StartsWithSegments("/api/auth");

    public static ApiResponse<T> Success<T>(T data, HttpContext context) =>
        new(true, data, null, RequestCorrelation.GetId(context));

    public static ApiResponse<object?> Success(HttpContext context) => Success<object?>(null, context);

    public static ApiResponse<object?> Failure(
        string code, string message, HttpContext context, IReadOnlyDictionary<string, string[]>? details = null) =>
        new(false, null, new ApiResponseError(code, message, details), RequestCorrelation.GetId(context));

    public static ApiResponse<object?> Failure(int statusCode, HttpContext context) => Failure(
        statusCode switch
        {
            StatusCodes.Status401Unauthorized => "invalid_access_token",
            StatusCodes.Status403Forbidden => "forbidden",
            StatusCodes.Status404NotFound => "not_found",
            StatusCodes.Status405MethodNotAllowed => "method_not_allowed",
            StatusCodes.Status415UnsupportedMediaType => "unsupported_media_type",
            StatusCodes.Status429TooManyRequests => "rate_limit_exceeded",
            _ => "http_error"
        },
        ReasonPhrases.GetReasonPhrase(statusCode),
        context);
}
