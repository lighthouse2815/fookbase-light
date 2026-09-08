using Fookbase.Api.Modules.Users.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Fookbase.Api.Shared.Contracts.Identity;
using Fookbase.Api.Modules.Users.Services;
using Fookbase.Api.Modules.Users.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Users.Api.IntegrationTests;

public sealed class UserProfileEndpointsTests(UsersApiFactory factory)
    : IClassFixture<UsersApiFactory>
{
    [Fact]
    public async Task User_registered_event_creates_profile_and_duplicate_is_idempotent()
    {
        var integrationEvent = CreateEvent();

        var firstCreated = await HandleAsync(integrationEvent);
        var duplicateCreated = await HandleAsync(integrationEvent);

        Assert.True(firstCreated);
        Assert.False(duplicateCreated);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var profile = await dbContext.UserProfiles.SingleAsync(
            item => item.UserId == integrationEvent.UserId);

        Assert.Equal(integrationEvent.Username, profile.Username);
        Assert.Equal(integrationEvent.Username, profile.DisplayName);
        Assert.Null(profile.Bio);
        Assert.Equal(1, await dbContext.InboxMessages.CountAsync(
            item => item.EventId == integrationEvent.EventId));
        Assert.Equal(1, await dbContext.UserProfiles.CountAsync(
            item => item.UserId == integrationEvent.UserId));
    }

    [Fact]
    public async Task Get_by_id_returns_public_profile()
    {
        var integrationEvent = CreateEvent();
        await HandleAsync(integrationEvent);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/users/{integrationEvent.UserId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal(integrationEvent.UserId, profile.UserId);
        Assert.Equal(integrationEvent.Username, profile.DisplayName);
    }

    [Fact]
    public async Task Get_me_without_access_token_returns_unauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_me_with_valid_access_token_returns_own_profile()
    {
        var integrationEvent = CreateEvent();
        await HandleAsync(integrationEvent);
        using var client = CreateAuthenticatedClient(integrationEvent.UserId);

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal(integrationEvent.UserId, profile.UserId);
    }

    [Fact]
    public async Task Patch_me_updates_only_authenticated_users_profile()
    {
        var userA = CreateEvent();
        var userB = CreateEvent();
        await HandleAsync(userA);
        await HandleAsync(userB);
        using var client = CreateAuthenticatedClient(userA.UserId);
        var request = new UpdateUserProfileRequest(
            "User A Display",
            "User A bio",
            new DateOnly(2000, 1, 2),
            "Da Nang");

        var response = await client.PatchAsJsonAsync("/api/users/me", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(updated);
        Assert.Equal(userA.UserId, updated.UserId);
        Assert.Equal(request.DisplayName, updated.DisplayName);
        Assert.Equal(request.Bio, updated.Bio);
        Assert.Equal(request.DateOfBirth, updated.DateOfBirth);
        Assert.Equal(request.CurrentCity, updated.CurrentCity);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var otherProfile = await dbContext.UserProfiles.AsNoTracking().SingleAsync(
            item => item.UserId == userB.UserId);
        Assert.Equal(userB.Username, otherProfile.DisplayName);
        Assert.Null(otherProfile.Bio);
    }

    private async Task<bool> HandleAsync(UserRegisteredIntegrationEvent integrationEvent)
    {
        using var scope = factory.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IUserRegisteredEventHandler>();
        return await handler.HandleAsync(integrationEvent);
    }

    private HttpClient CreateAuthenticatedClient(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var issuer = configuration["Jwt:Issuer"]!;
        var audience = configuration["Jwt:Audience"]!;
        var signingKey = configuration["Jwt:SigningKey"]!;
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer,
            audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            notBefore: now.AddSeconds(-1),
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private static UserRegisteredIntegrationEvent CreateEvent()
    {
        var suffix = Guid.NewGuid().ToString("N")[..16];
        return new UserRegisteredIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            $"user_{suffix}",
            DateTimeOffset.UtcNow);
    }
}
