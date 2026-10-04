using System.Security.Claims;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.Authentication;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed record GoogleExternalIdentity(string ProviderKey, string Email, bool EmailVerified,
    string? Client = null, string? CodeChallenge = null, string? State = null);

public interface IGoogleExternalIdentityReader
{
    Task<GoogleExternalIdentity> ReadAsync(
        HttpContext context,
        CancellationToken cancellationToken = default,
        bool mobile = false);
}

public sealed class GoogleExternalIdentityReader(IAuthenticationService authenticationService)
    : IGoogleExternalIdentityReader
{
    public async Task<GoogleExternalIdentity> ReadAsync(
        HttpContext context,
        CancellationToken cancellationToken = default,
        bool mobile = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var authentication = await authenticationService.AuthenticateAsync(context, IdentityModuleConstants.ExternalLogin.ExternalScheme);
        await authenticationService.SignOutAsync(context, IdentityModuleConstants.ExternalLogin.ExternalScheme, null);
        if (!authentication.Succeeded || authentication.Principal is null)
        {
            throw InvalidIdentity(mobile);
        }

        var providerKey = authentication.Principal.FindFirstValue("sub");
        var email = authentication.Principal.FindFirstValue("email");
        var verifiedValue = authentication.Principal.FindFirstValue("email_verified");
        if (string.IsNullOrWhiteSpace(providerKey) || string.IsNullOrWhiteSpace(email) ||
            !bool.TryParse(verifiedValue, out var emailVerified))
        {
            throw InvalidIdentity(mobile);
        }

        return new GoogleExternalIdentity(providerKey, email, emailVerified,
            authentication.Properties?.GetString("fookbase.client"),
            authentication.Properties?.GetString("fookbase.code_challenge"),
            authentication.Properties?.GetString("fookbase.state"));
    }

    internal static BusinessException InvalidIdentity(bool mobile) =>
        new(new ApplicationError(
            mobile ? "invalid_mobile_google_login" : "invalid_google_identity",
            mobile ? "The mobile Google sign-in is invalid or expired." : "The Google identity is invalid.",
            ApplicationErrorType.UNAUTHORIZED));
}
