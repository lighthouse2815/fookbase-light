using Fookbase.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Http;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class TestGoogleExternalIdentityReader : IGoogleExternalIdentityReader
{
    public Task<GoogleExternalIdentity> ReadAsync(
        HttpContext context,
        CancellationToken cancellationToken = default,
        bool mobile = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var providerKey = context.Request.Headers["X-Test-Google-Sub"].ToString();
        var email = context.Request.Headers["X-Test-Google-Email"].ToString();
        var verified = context.Request.Headers["X-Test-Google-Email-Verified"].ToString();
        if (string.IsNullOrWhiteSpace(providerKey) || string.IsNullOrWhiteSpace(email) ||
            !bool.TryParse(verified, out var emailVerified))
        {
            throw GoogleExternalIdentityReader.InvalidIdentity(mobile);
        }

        return Task.FromResult(
            new GoogleExternalIdentity(providerKey, email, emailVerified,
                context.Request.Headers["X-Test-Google-Client"].FirstOrDefault(),
                context.Request.Headers["X-Test-Google-Challenge"].FirstOrDefault(),
                context.Request.Headers["X-Test-Google-State"].FirstOrDefault()));
    }
}
