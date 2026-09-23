using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Identity.Controllers;

[ApiController]
public abstract class IdentityControllerBase : ControllerBase
{
    protected bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);

    protected static IResult InvalidAccessToken() =>
        new ApplicationError(
            ErrorCode.InvalidAccessToken,
            ErrorCode.InvalidAccessToken.Message,
            ApplicationErrorType.Unauthorized).ToHttpResult();
}
