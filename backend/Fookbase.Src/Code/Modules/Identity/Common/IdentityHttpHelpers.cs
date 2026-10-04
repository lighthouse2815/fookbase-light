using Fookbase.Api.Shared.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Shared.ErrorHandling;

namespace Fookbase.Api.Modules.Identity.Common;

internal static class IdentityHttpHelpers
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)
            ? userId
            : throw new BusinessException(new ApplicationError(
                ErrorCode.InvalidAccessToken,
                ErrorCode.InvalidAccessToken.Message,
                ApplicationErrorType.UNAUTHORIZED));
}
