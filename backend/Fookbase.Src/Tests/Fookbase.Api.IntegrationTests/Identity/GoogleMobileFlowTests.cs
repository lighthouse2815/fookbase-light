using System.Security.Cryptography;
using System.Text;
using Fookbase.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Fookbase.Identity.Api.IntegrationTests;

public class GoogleMobileFlowTests
{
    [Fact]
    public void Protected_completion_requires_the_original_verifier()
    {
        var flow = new GoogleMobileFlow(new EphemeralDataProtectionProvider());
        var verifier = new string('a', 64);
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var token = flow.Protect("one-time-code", challenge);
        Assert.Equal("one-time-code", flow.Unprotect(token, verifier));
        Assert.Null(flow.Unprotect(token, new string('b', 64)));
        Assert.Null(flow.Unprotect(token + "tampered", verifier));
        Assert.Null(flow.Unprotect("one-time-code", verifier));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("https://evil.example/callback")]
    public void Invalid_challenges_are_rejected(string challenge)
    {
        Assert.False(GoogleMobileFlow.IsValidChallenge(challenge));
    }
}
