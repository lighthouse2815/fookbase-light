using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Shared.Common;

internal static class ApplicationResultExtensions
{
    public static IResult ToHttpResult(this ApplicationError error)
    {
        var statusCode = error.Type switch
        {
            ApplicationErrorType.Validation => StatusCodes.Status400BadRequest,
            ApplicationErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ApplicationErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ApplicationErrorType.NotFound => StatusCodes.Status404NotFound,
            ApplicationErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = error.Type.ToString(),
            Detail = error.Message
        };

        problem.Extensions["code"] = error.Code;
        if (error.Details is not null)
        {
            problem.Extensions["errors"] = error.Details;
        }

        return Results.Problem(problem);
    }
}
