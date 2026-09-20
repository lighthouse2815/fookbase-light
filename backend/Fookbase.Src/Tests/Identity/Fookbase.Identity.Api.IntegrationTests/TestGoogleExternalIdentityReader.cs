using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Http;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class TestGoogleExternalIdentityReader : IGoogleExternalIdentityReader
{
    public Task<ApplicationResult<GoogleExternalIdentity>> ReadAsync(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var providerKey = context.Request.Headers["X-Test-Google-Sub"].ToString();
        var email = context.Request.Headers["X-Test-Google-Email"].ToString();
        var verified = context.Request.Headers["X-Test-Google-Email-Verified"].ToString();
        if (string.IsNullOrWhiteSpace(providerKey) || string.IsNullOrWhiteSpace(email) ||
            !bool.TryParse(verified, out var emailVerified))
        {
            return Task.FromResult(ApplicationResult<GoogleExternalIdentity>.Failure(new ApplicationError(
                "invalid_google_identity",
                "The Google identity is invalid.",
                ApplicationErrorType.Unauthorized)));
        }

        return Task.FromResult(ApplicationResult<GoogleExternalIdentity>.Success(
            new GoogleExternalIdentity(providerKey, email, emailVerified,
                context.Request.Headers["X-Test-Google-Client"].FirstOrDefault(),
                context.Request.Headers["X-Test-Google-Challenge"].FirstOrDefault(),
                context.Request.Headers["X-Test-Google-State"].FirstOrDefault())));
    }
}
