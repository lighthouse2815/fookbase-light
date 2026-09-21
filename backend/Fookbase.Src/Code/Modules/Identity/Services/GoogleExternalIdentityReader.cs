using System.Security.Claims;
using Fookbase.Api.Modules.Identity.Common;
using Microsoft.AspNetCore.Authentication;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed record GoogleExternalIdentity(string ProviderKey, string Email, bool EmailVerified,
    string? Client = null, string? CodeChallenge = null, string? State = null);

public interface IGoogleExternalIdentityReader
{
    Task<ApplicationResult<GoogleExternalIdentity>> ReadAsync(
        HttpContext context,
        CancellationToken cancellationToken = default);
}

public sealed class GoogleExternalIdentityReader(IAuthenticationService authenticationService)
    : IGoogleExternalIdentityReader
{
    private const string ExternalScheme = "GoogleExternal";

    public async Task<ApplicationResult<GoogleExternalIdentity>> ReadAsync(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var authentication = await authenticationService.AuthenticateAsync(context, ExternalScheme);
        await authenticationService.SignOutAsync(context, ExternalScheme, null);
        if (!authentication.Succeeded || authentication.Principal is null)
        {
            return InvalidIdentity();
        }

        var providerKey = authentication.Principal.FindFirstValue("sub");
        var email = authentication.Principal.FindFirstValue("email");
        var verifiedValue = authentication.Principal.FindFirstValue("email_verified");
        if (string.IsNullOrWhiteSpace(providerKey) || string.IsNullOrWhiteSpace(email) ||
            !bool.TryParse(verifiedValue, out var emailVerified))
        {
            return InvalidIdentity();
        }

        return ApplicationResult<GoogleExternalIdentity>.Success(
            new GoogleExternalIdentity(providerKey, email, emailVerified,
                authentication.Properties?.GetString("fookbase.client"),
                authentication.Properties?.GetString("fookbase.code_challenge"),
                authentication.Properties?.GetString("fookbase.state")));
    }

    private static ApplicationResult<GoogleExternalIdentity> InvalidIdentity() =>
        ApplicationResult<GoogleExternalIdentity>.Failure(new ApplicationError(
            "invalid_google_identity",
            "The Google identity is invalid.",
            ApplicationErrorType.Unauthorized));
}
