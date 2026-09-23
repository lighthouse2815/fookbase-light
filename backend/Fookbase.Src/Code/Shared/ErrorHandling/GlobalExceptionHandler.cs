using System.Text.Json;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Shared.Observability;
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
        if (exception is BusinessException businessException)
        {
            if (ApiResponse.AppliesTo(httpContext))
            {
                var failure = businessException.Error;
                await Results.Json(
                    ApiResponse.Failure(failure.Code, failure.Message, httpContext, failure.Details),
                    statusCode: failure.ToStatusCode()).ExecuteAsync(httpContext);
                return true;
            }

            var businessProblem = businessException.Error.ToProblemDetails();
            businessProblem.Extensions["requestId"] = RequestCorrelation.GetId(httpContext);
            await Results.Problem(businessProblem).ExecuteAsync(httpContext);
            return true;
        }

        var isBadRequest = exception is BadHttpRequestException or JsonException or InvalidDataException;
        if (isBadRequest)
        {
            logger.LogWarning("An invalid HTTP request was rejected. RequestId: {RequestId}",
                RequestCorrelation.GetId(httpContext));
        }
        else
        {
            logger.LogError(exception, "An unexpected error occurred while processing the request. RequestId: {RequestId}",
                RequestCorrelation.GetId(httpContext));
        }

        var error = isBadRequest
            ? ErrorCode.InvalidRequest
            : ErrorCode.InternalServerError;
        var statusCode = isBadRequest
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status500InternalServerError;
        if (ApiResponse.AppliesTo(httpContext))
        {
            await Results.Json(ApiResponse.Failure(error.Code, error.Message, httpContext), statusCode: statusCode)
                .ExecuteAsync(httpContext);
            return true;
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = isBadRequest ? "Bad request" : "Internal server error",
            Detail = error.Message
        };
        problem.Extensions["code"] = error.Code;
        problem.Extensions["requestId"] = RequestCorrelation.GetId(httpContext);

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
