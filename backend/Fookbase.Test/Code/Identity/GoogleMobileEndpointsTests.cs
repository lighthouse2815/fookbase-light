using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fookbase.Identity.Api.IntegrationTests;

public class GoogleMobileEndpointsTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    [Theory]
    [InlineData("/api/auth/google/callback?client=web", "invalid_google_identity")]
    [InlineData("/api/auth/google/mobile/callback", "invalid_mobile_google_login")]
    public async Task Missing_external_identity_preserves_the_callback_error(string path, string code)
    {
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("GoogleAuthentication:MobileCallbackUrl", "https://mobile.example.test/auth/callback");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGoogleExternalIdentityReader>();
                services.AddScoped<IGoogleExternalIdentityReader, GoogleExternalIdentityReader>();
            });
        });
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var problem = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, problem.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(problem.RootElement.GetProperty("requestId").GetString()));
        Assert.Null(response.Headers.Location);
    }

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
        Assert.NotNull((await result.Content.ReadApiDataAsync<AuthenticationResponse>())?.AccessToken);
        var replay = await client.PostAsJsonAsync("/api/auth/google/mobile/exchange", new { code, verifier });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task Zola_mobile_code_uses_its_own_callback_and_client_binding()
    {
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("GoogleAuthentication:MobileCallbackUrl", "https://mobile.example.test/auth/callback");
            builder.UseSetting("GoogleAuthentication:ZolaMobileCallbackUrl", "https://zola.example.test/auth/callback");
        });
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var verifier = new string('a', 64);
        AddIdentity(client, verifier, "zola-mobile");

        var callback = await client.GetAsync("/api/auth/google/mobile/callback");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("zola.example.test", callback.Headers.Location!.Host);
        var code = QueryHelpers.ParseQuery(callback.Headers.Location.Query)["code"].ToString();
        var wrongClient = await client.PostAsJsonAsync("/api/auth/google/mobile/exchange", new
        {
            code,
            verifier,
            client = "mobile"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongClient.StatusCode);
        var exchange = await client.PostAsJsonAsync("/api/auth/google/mobile/exchange", new
        {
            code,
            verifier,
            client = "zola-mobile"
        });
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
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
    private static void AddIdentity(HttpClient client, string verifier, string clientName = "mobile")
    {
        client.DefaultRequestHeaders.Add("X-Test-Google-Sub", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-Test-Google-Email", $"mobile-{Guid.NewGuid():N}@example.test");
        client.DefaultRequestHeaders.Add("X-Test-Google-Email-Verified", "true");
        client.DefaultRequestHeaders.Add("X-Test-Google-Client", clientName);
        client.DefaultRequestHeaders.Add("X-Test-Google-Challenge", WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
        client.DefaultRequestHeaders.Add("X-Test-Google-State", new string('s', 43));
    }
}
