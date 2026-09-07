using Fookbase.Users.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Users.Api.Endpoints;

internal static class ApplicationResultExtensions
{
    public static IResult ToHttpResult(this ApplicationError error)
    {
        var statusCode = error.Type switch
        {
            ApplicationErrorType.Validation => StatusCodes.Status400BadRequest,
            ApplicationErrorType.NotFound => StatusCodes.Status404NotFound,
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
