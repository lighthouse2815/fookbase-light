using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Shared.ErrorHandling;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var isBadRequest = exception is BadHttpRequestException or JsonException or InvalidDataException;
        if (isBadRequest)
        {
            logger.LogWarning("An invalid HTTP request was rejected.");
        }
        else
        {
            logger.LogError(exception, "An unexpected error occurred while processing the request.");
        }

        var statusCode = isBadRequest
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status500InternalServerError;
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = isBadRequest ? "Bad request" : "Internal server error",
            Detail = isBadRequest
                ? "The request body is invalid."
                : "An unexpected error occurred."
        };
        problem.Extensions["code"] = isBadRequest
            ? "invalid_request"
            : "internal_server_error";

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
