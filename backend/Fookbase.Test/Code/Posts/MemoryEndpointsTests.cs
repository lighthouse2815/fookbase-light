using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Memories.Controllers;
using Fookbase.Api.Modules.Memories.DTOs.Responses;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class MemoryEndpointsTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Fact]
    public void Today_route_uses_controller_and_preserves_method_and_authorization()
    {
        using var client = factory.CreateClient();
        var endpoint = Assert.Single(factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>(), item => item.RoutePattern.RawText?.Trim('/') == "api/memories/today");

        Assert.Equal(["GET"], endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
        Assert.Equal(typeof(MemoriesController), endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()!.ControllerTypeInfo.AsType());
        Assert.NotNull(endpoint.Metadata.GetMetadata<IAuthorizeData>());
        Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
    }

    [Fact]
    public async Task Today_rejects_anonymous_requests()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/memories/today");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-user-id")]
    public async Task Today_rejects_tokens_without_valid_user_id(string? userId)
    {
        using var client = CreateAuthenticatedClient(userId);
        using var response = await client.GetAsync("/api/memories/today");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Today_returns_only_own_active_prior_year_profile_posts()
    {
        var owner = await CreateUserAsync();
        var other = await CreateUserAsync();
        var now = DateTimeOffset.UtcNow;
        var eligible = new Post(Guid.NewGuid(), owner, "memory", PostPrivacy.PUBLIC, now.AddYears(-1));
        var deleted = new Post(Guid.NewGuid(), owner, "deleted", PostPrivacy.PUBLIC, now.AddYears(-1));
        deleted.Delete(now);
        var otherPost = new Post(Guid.NewGuid(), other, "other", PostPrivacy.PUBLIC, now.AddYears(-1));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.Posts.AddRange(eligible, deleted, otherPost);
            await db.SaveChangesAsync();
        }

        using var client = CreateAuthenticatedClient(owner.ToString());
        var response = await client.GetAsync("/api/memories/today");
        var memories = await response.Content.ReadFromJsonAsync<MemoryTodayResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(memories);
        Assert.Contains(memories.Years.SelectMany(year => year.Items), item => item.Id == eligible.Id);
        Assert.DoesNotContain(memories.Years.SelectMany(year => year.Items), item => item.Id == deleted.Id || item.Id == otherPost.Id);
    }

    private async Task<Guid> CreateUserAsync()
    {
        var id = Guid.NewGuid();
        var username = "memory_" + id.ToString("N")[..16];
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.Users.Add(new User(id, $"{username}@example.com", username, DateTimeOffset.UtcNow));
        db.UserProfiles.Add(new UserProfile(id, username, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return id;
    }

    private HttpClient CreateAuthenticatedClient(string? userId)
    {
        using var scope = factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var now = DateTime.UtcNow;
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) };
        if (userId is not null)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, userId));
        }
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"],
            claims,
            now.AddSeconds(-1), now.AddMinutes(5), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
}
