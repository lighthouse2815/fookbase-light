using Fookbase.Api.Modules.Users.DTOs.Requests;
using Fookbase.Api.Modules.Users.DTOs.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Fookbase.Api.Modules.Users.Data;
using Fookbase.Api.Modules.Users.Services;
using Fookbase.Api.Modules.Media.Data;
using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Users.Api.IntegrationTests;

public sealed class UserProfileEndpointsTests(UsersApiFactory factory)
    : IClassFixture<UsersApiFactory>
{
    [Fact]
    public async Task Profile_creation_is_idempotent()
    {
        var user = CreateUser();

        var firstCreated = await EnsureProfileAsync(user);
        var duplicateCreated = await EnsureProfileAsync(user);

        Assert.True(firstCreated);
        Assert.False(duplicateCreated);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var profile = await dbContext.UserProfiles.SingleAsync(
            item => item.UserId == user.Id);

        Assert.Equal(user.Username, profile.Username);
        Assert.Equal(user.Username, profile.DisplayName);
        Assert.Null(profile.Bio);
        Assert.Equal(1, await dbContext.UserProfiles.CountAsync(
            item => item.UserId == user.Id));
    }

    [Fact]
    public async Task Get_by_id_returns_public_profile()
    {
        var user = CreateUser();
        await EnsureProfileAsync(user);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/users/{user.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal(user.Id, profile.UserId);
        Assert.Equal(user.Username, profile.DisplayName);
    }

    [Fact]
    public async Task Search_returns_matching_profiles_with_pagination()
    {
        var searchTerm = $"security_{Guid.NewGuid():N}"[..25];
        var matchingUser = new UserSeed(Guid.NewGuid(), searchTerm);
        var otherUser = new UserSeed(Guid.NewGuid(), $"frontend_{Guid.NewGuid():N}"[..25]);
        await EnsureProfileAsync(matchingUser);
        await EnsureProfileAsync(otherUser);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/users/search?query={searchTerm}&offset=0&limit=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<UserProfileResponse>>();
        Assert.NotNull(page);
        Assert.Equal(1, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(matchingUser.Id, page.Items[0].UserId);
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
        var user = CreateUser();
        await EnsureProfileAsync(user);
        using var client = CreateAuthenticatedClient(user.Id);

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal(user.Id, profile.UserId);
    }

    [Fact]
    public async Task Patch_me_updates_only_authenticated_users_profile()
    {
        var userA = CreateUser();
        var userB = CreateUser();
        await EnsureProfileAsync(userA);
        await EnsureProfileAsync(userB);
        using var client = CreateAuthenticatedClient(userA.Id);
        var request = new UpdateUserProfileRequest(
            "User A Display",
            "User A bio",
            new DateOnly(2000, 1, 2),
            "Da Nang");

        var response = await client.PatchAsJsonAsync("/api/users/me", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(updated);
        Assert.Equal(userA.Id, updated.UserId);
        Assert.Equal(request.DisplayName, updated.DisplayName);
        Assert.Equal(request.Bio, updated.Bio);
        Assert.Equal(request.DateOfBirth, updated.DateOfBirth);
        Assert.Equal(request.CurrentCity, updated.CurrentCity);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var otherProfile = await dbContext.UserProfiles.AsNoTracking().SingleAsync(
            item => item.UserId == userB.Id);
        Assert.Equal(userB.Username, otherProfile.DisplayName);
        Assert.Null(otherProfile.Bio);
    }

    [Fact]
    public async Task Active_avatar_and_cover_media_are_synchronized_and_cannot_be_deleted()
    {
        var user = CreateUser();
        await EnsureProfileAsync(user);
        var avatar = await CreateReadyImageAsync(user.Id);
        var cover = await CreateReadyImageAsync(user.Id);
        var replacementAvatar = await CreateReadyImageAsync(user.Id);
        using var client = CreateAuthenticatedClient(user.Id);

        var update = await client.PatchAsJsonAsync(
            "/api/users/me",
            new UpdateUserProfileRequest(null, null, null, null, avatar, cover));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/media/{avatar}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/media/{cover}")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var mediaDb = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            Assert.Contains(await mediaDb.ProfileMediaReferences.AsNoTracking().ToListAsync(), reference =>
                reference.UserId == user.Id &&
                reference.Slot == ProfileMediaSlot.Avatar &&
                reference.MediaId == avatar);
            Assert.Contains(await mediaDb.ProfileMediaReferences.AsNoTracking().ToListAsync(), reference =>
                reference.UserId == user.Id &&
                reference.Slot == ProfileMediaSlot.Cover &&
                reference.MediaId == cover);
        }

        var replaceAvatar = await client.PatchAsJsonAsync(
            "/api/users/me",
            new UpdateUserProfileRequest(null, null, null, null, replacementAvatar));
        Assert.Equal(HttpStatusCode.OK, replaceAvatar.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/media/{avatar}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/media/{cover}")).StatusCode);
        using var verification = factory.Services.CreateScope();
        var profile = await verification.ServiceProvider.GetRequiredService<UsersDbContext>()
            .UserProfiles.AsNoTracking()
            .SingleAsync(item => item.UserId == user.Id);
        Assert.Equal(replacementAvatar, profile.AvatarMediaId);
        Assert.Equal(cover, profile.CoverMediaId);
    }

    private async Task<bool> EnsureProfileAsync(UserSeed user)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<UserProfileService>();
        var existed = await scope.ServiceProvider.GetRequiredService<UsersDbContext>()
            .UserProfiles.AnyAsync(profile => profile.UserId == user.Id);
        await service.EnsureCreatedAsync(user.Id, user.Username);
        return !existed;
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

    private static UserSeed CreateUser()
    {
        var suffix = Guid.NewGuid().ToString("N")[..16];
        return new UserSeed(Guid.NewGuid(), $"user_{suffix}");
    }

    private async Task<Guid> CreateReadyImageAsync(Guid ownerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var mediaDb = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var now = DateTimeOffset.UtcNow;
        var mediaId = Guid.NewGuid();
        var asset = MediaAsset.CreatePending(
            mediaId,
            ownerUserId,
            MediaType.Image,
            $"{ownerUserId:N}/{mediaId:N}.png",
            "profile.png",
            "image/png",
            11,
            now,
            now.AddMinutes(5));
        asset.MarkReady(11, now);
        mediaDb.MediaAssets.Add(asset);
        await mediaDb.SaveChangesAsync();
        return mediaId;
    }

    private sealed record UserSeed(Guid Id, string Username);
}
