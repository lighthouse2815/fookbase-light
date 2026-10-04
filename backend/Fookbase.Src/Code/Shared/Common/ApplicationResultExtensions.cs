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
            Title = error.Type.ToApiName(),
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
        ApplicationErrorType.VALIDATION => StatusCodes.Status400BadRequest,
        ApplicationErrorType.UNAUTHORIZED => StatusCodes.Status401Unauthorized,
        ApplicationErrorType.FORBIDDEN => StatusCodes.Status403Forbidden,
        ApplicationErrorType.NOT_FOUND => StatusCodes.Status404NotFound,
        ApplicationErrorType.CONFLICT => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };
}
