using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class PageMediaRequestValidationTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Theory]
    [InlineData("{\"avatarMediaId\":\"00000000-0000-0000-0000-000000000000\"}", "AvatarMediaId")]
    [InlineData("{\"coverMediaId\":\"00000000-0000-0000-0000-000000000000\"}", "CoverMediaId")]
    [InlineData("{\"avatarMediaId\":\"00000000-0000-0000-0000-000000000001\",\"removeAvatar\":true}", "AvatarMediaId")]
    [InlineData("{\"coverMediaId\":\"00000000-0000-0000-0000-000000000001\",\"removeCover\":true}", "CoverMediaId")]
    public async Task Invalid_media_updates_are_rejected_before_loading_the_page(string body, string member)
    {
        using var client = CreateClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PatchAsync($"/api/pages/{Guid.NewGuid()}/media", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation_failed", document.RootElement.GetProperty("code").GetString());
        Assert.NotEmpty(document.RootElement.GetProperty("errors").GetProperty(member).EnumerateArray());
    }

    [Fact]
    public async Task Conflicting_avatar_and_cover_updates_report_both_fields()
    {
        using var client = CreateClient();
        using var response = await client.PatchAsJsonAsync($"/api/pages/{Guid.NewGuid()}/media", new
        {
            avatarMediaId = Guid.NewGuid(),
            coverMediaId = Guid.NewGuid(),
            removeAvatar = true,
            removeCover = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors");
        Assert.NotEmpty(errors.GetProperty("AvatarMediaId").EnumerateArray());
        Assert.NotEmpty(errors.GetProperty("CoverMediaId").EnumerateArray());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"avatarMediaId\":null,\"coverMediaId\":null}")]
    [InlineData("{\"avatarMediaId\":\"00000000-0000-0000-0000-000000000001\"}")]
    [InlineData("{\"coverMediaId\":\"00000000-0000-0000-0000-000000000001\"}")]
    [InlineData("{\"avatarMediaId\":\"00000000-0000-0000-0000-000000000001\",\"coverMediaId\":\"00000000-0000-0000-0000-000000000002\"}")]
    [InlineData("{\"removeAvatar\":true}")]
    [InlineData("{\"removeCover\":true}")]
    [InlineData("{\"removeAvatar\":true,\"removeCover\":true}")]
    [InlineData("{\"avatarMediaId\":\"00000000-0000-0000-0000-000000000001\",\"removeCover\":true}")]
    [InlineData("{\"coverMediaId\":\"00000000-0000-0000-0000-000000000001\",\"removeAvatar\":true}")]
    public async Task Valid_partial_media_updates_reach_the_page_service(string body)
    {
        using var client = CreateClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PatchAsync($"/api/pages/{Guid.NewGuid()}/media", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateClient()
    {
        using var scope = factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"],
            [new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())], now.AddSeconds(-1), now.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!)),
                SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
}
