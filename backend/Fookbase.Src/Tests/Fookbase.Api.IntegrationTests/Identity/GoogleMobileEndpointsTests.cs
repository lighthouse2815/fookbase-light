using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;

namespace Fookbase.Identity.Api.IntegrationTests;

public class GoogleMobileEndpointsTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    [Fact]
    public async Task Mobile_code_requires_verifier_and_is_single_use()
    {
        using var app = factory.WithWebHostBuilder(builder => builder.UseSetting("GoogleAuthentication:MobileCallbackUrl", "https://mobile.example.test/auth/callback"));
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var verifier = new string('a', 64);
        AddIdentity(client, verifier);
        var callback = await client.GetAsync("/api/auth/google/mobile/callback");
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        var location = callback.Headers.Location!;
        Assert.Equal("mobile.example.test", location.Host);
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal(new string('s', 43), query["state"]);
        var code = query["code"].ToString();
        var wrong = await client.PostAsJsonAsync("/api/auth/google/mobile/exchange", new { code, verifier = new string('b', 64) });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        var result = await client.PostAsJsonAsync("/api/auth/google/mobile/exchange", new { code, verifier });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull((await result.Content.ReadFromJsonAsync<AuthenticationResponse>())?.AccessToken);
        var replay = await client.PostAsJsonAsync("/api/auth/google/mobile/exchange", new { code, verifier });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }
    [Fact]
    public async Task Native_identity_cannot_bypass_pkce_using_web_callback()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        AddIdentity(client, new string('a', 64));
        var result = await client.GetAsync("/api/auth/google/callback?client=web");
        Assert.False(result.IsSuccessStatusCode);
        Assert.Null(result.Headers.Location);
    }
    private static void AddIdentity(HttpClient client, string verifier)
    {
        client.DefaultRequestHeaders.Add("X-Test-Google-Sub", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-Test-Google-Email", $"mobile-{Guid.NewGuid():N}@example.test");
        client.DefaultRequestHeaders.Add("X-Test-Google-Email-Verified", "true");
        client.DefaultRequestHeaders.Add("X-Test-Google-Client", "mobile");
        client.DefaultRequestHeaders.Add("X-Test-Google-Challenge", WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
        client.DefaultRequestHeaders.Add("X-Test-Google-State", new string('s', 43));
    }
}
