using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Memories.DTOs.Responses;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class MemoryEndpointsTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Fact]
    public async Task Today_returns_only_own_active_prior_year_profile_posts()
    {
        var owner = await CreateUserAsync();
        var other = await CreateUserAsync();
        var now = DateTimeOffset.UtcNow;
        var eligible = Post.Create(Guid.NewGuid(), owner, "memory", PostPrivacy.Public, now.AddYears(-1));
        var deleted = Post.Create(Guid.NewGuid(), owner, "deleted", PostPrivacy.Public, now.AddYears(-1));
        deleted.Delete(now);
        var otherPost = Post.Create(Guid.NewGuid(), other, "other", PostPrivacy.Public, now.AddYears(-1));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.Posts.AddRange(eligible, deleted, otherPost);
            await db.SaveChangesAsync();
        }

        using var client = CreateAuthenticatedClient(owner);
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
        db.UserProfiles.Add(UserProfile.Create(id, username, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return id;
    }

    private HttpClient CreateAuthenticatedClient(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"],
            [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())],
            now.AddSeconds(-1), now.AddMinutes(5), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
}
