using Fookbase.Api.Modules.Friends.Services.Common;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Friends.Endpoints;

internal static class ApplicationResultExtensions
{
    public static IResult ToHttpResult(this ApplicationError error)
    {
        var statusCode = error.Type switch
        {
            ApplicationErrorType.Validation => StatusCodes.Status400BadRequest,
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
        return Results.Problem(problem);
    }
}
