using Fookbase.Api.Shared.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Shared.ErrorHandling;

namespace Fookbase.Api.Modules.Identity.Common;

internal static class IdentityHttpHelpers
{
    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);

    public static IResult InvalidAccessToken() =>
        new ApplicationError(
            ErrorCode.InvalidAccessToken,
            ErrorCode.InvalidAccessToken.Message,
            ApplicationErrorType.Unauthorized).ToHttpResult();
}
