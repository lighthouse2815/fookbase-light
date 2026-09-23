using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Shared.Common;

internal static class ApplicationResultExtensions
{
    public static IResult ToHttpResult(this ApplicationError error) => Results.Problem(error.ToProblemDetails());

    public static ProblemDetails ToProblemDetails(this ApplicationError error)
    {
        var problem = new ProblemDetails
        {
            Status = error.ToStatusCode(),
            Title = error.Type.ToString(),
            Detail = error.Message
        };

        problem.Extensions["code"] = error.Code;
        if (error.Details is not null)
        {
            problem.Extensions["errors"] = error.Details;
        }

        return problem;
    }

    public static int ToStatusCode(this ApplicationError error) => error.Type switch
    {
        ApplicationErrorType.Validation => StatusCodes.Status400BadRequest,
        ApplicationErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ApplicationErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ApplicationErrorType.NotFound => StatusCodes.Status404NotFound,
        ApplicationErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };
}
