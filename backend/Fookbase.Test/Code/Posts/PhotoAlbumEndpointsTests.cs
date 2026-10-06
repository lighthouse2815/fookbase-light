using Fookbase.Api.Modules.Media.Domain.Enums;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Fookbase.Api.Modules.Friends.Entities;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Photos.Domain.Enums;
using Fookbase.Api.Modules.Photos.DTOs.Responses;
using Fookbase.Api.Modules.Photos.Entities;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class PhotoAlbumEndpointsTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Fact]
    public async Task Custom_album_and_caption_can_be_created_updated_and_deleted()
    {
        var ownerId = await CreateUserAsync();
        var mediaId = await SeedReadyImageAsync(ownerId);
        using var owner = CreateAuthenticatedClient(ownerId, allowAutoRedirect: false);
        using var create = await owner.PostAsJsonAsync("/api/albums", new
        {
            name = " Travel photos ", description = " First trip ", privacy = "only_me"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var album = (await create.Content.ReadFromJsonAsync<PhotoAlbumResponse>())!;
        Assert.Equal($"/api/albums/{album.Id}", create.Headers.Location!.OriginalString);
        Assert.Equal("Travel photos", album.Name);
        Assert.Equal("First trip", album.Description);
        Assert.Equal("custom", album.AlbumType);
        Assert.Equal("onlyme", album.Privacy);
        Assert.Equal(ownerId, album.OwnerUserId);
        Assert.True(album.CanManage);

        var mediaPath = $"/api/albums/{album.Id}/media/{mediaId}";
        using var add = await owner.PostAsJsonAsync($"/api/albums/{album.Id}/media", new { mediaId });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        Assert.Equal(mediaPath, add.Headers.Location!.OriginalString);
        using var caption = await owner.PatchAsJsonAsync(mediaPath, new { caption = " First photo " });
        Assert.Equal(HttpStatusCode.OK, caption.StatusCode);
        Assert.Equal("First photo", (await caption.Content.ReadFromJsonAsync<AlbumMediaResponse>())!.Caption);
        using var detail = await owner.GetAsync(mediaPath);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var photo = (await detail.Content.ReadFromJsonAsync<PhotoDetailResponse>())!;
        Assert.Equal(mediaId, photo.MediaId);
        Assert.Equal(album.Id, photo.AlbumId);
        Assert.Equal(ownerId, photo.OwnerUserId);
        Assert.Equal("First photo", photo.Caption);
        using var access = await owner.GetAsync($"{mediaPath}/access");
        Assert.Equal(HttpStatusCode.Redirect, access.StatusCode);
        Assert.Equal(photo.Url, access.Headers.Location!.OriginalString);
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var hiddenPhoto = await anonymous.GetAsync(mediaPath);
        using var hiddenAccess = await anonymous.GetAsync($"{mediaPath}/access");
        Assert.Equal(HttpStatusCode.NotFound, hiddenPhoto.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, hiddenAccess.StatusCode);
        using var clearCaption = await owner.PatchAsJsonAsync(mediaPath, new { caption = (string?)null });
        Assert.Equal(HttpStatusCode.OK, clearCaption.StatusCode);
        Assert.Null((await clearCaption.Content.ReadFromJsonAsync<AlbumMediaResponse>())!.Caption);

        using var update = await owner.PatchAsJsonAsync($"/api/albums/{album.Id}", new
        {
            name = " New name ", description = (string?)null, privacy = "friends"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content.ReadFromJsonAsync<PhotoAlbumResponse>())!;
        Assert.Equal("New name", updated.Name);
        Assert.Null(updated.Description);
        Assert.Equal("friends", updated.Privacy);
        Assert.Equal(1, updated.PhotoCount);

        using var list = await owner.GetAsync($"/api/albums/{album.Id}/media?limit=1");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content.ReadFromJsonAsync<PhotoCursorPageResponse<AlbumMediaResponse>>())!;
        Assert.Equal(mediaId, Assert.Single(page.Items).MediaId);
        using var remove = await owner.DeleteAsync(mediaPath);
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        using var delete = await owner.DeleteAsync($"/api/albums/{album.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        using var missing = await owner.GetAsync($"/api/albums/{album.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Avatar_change_makes_profile_pictures_album_available()
    {
        var ownerId = await CreateUserAsync();
        var mediaId = await SeedReadyImageAsync(ownerId);
        using var client = CreateAuthenticatedClient(ownerId);

        var update = await client.PatchAsJsonAsync("/api/users/me", new { avatarMediaId = mediaId });
        var albums = await client.GetAsync($"/api/users/{ownerId}/albums");

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.OK, albums.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var album = await db.PhotoAlbums.SingleAsync(item => item.OwnerUserId == ownerId && item.AlbumType == PhotoAlbumType.PROFILE_PICTURES);
        Assert.True(await db.AlbumMedia.AnyAsync(item => item.AlbumId == album.Id && item.MediaId == mediaId));
    }

    [Fact]
    public async Task Friends_album_is_hidden_from_strangers_and_blocked_friends()
    {
        var ownerId = await CreateUserAsync();
        var friendId = await CreateUserAsync();
        var strangerId = await CreateUserAsync();
        var albumId = await SeedAlbumAsync(ownerId, PhotoAlbumPrivacy.FRIENDS);
        await AddFriendshipAsync(ownerId, friendId);
        using var friend = CreateAuthenticatedClient(friendId);
        using var stranger = CreateAuthenticatedClient(strangerId);

        Assert.Equal(HttpStatusCode.OK, (await friend.GetAsync($"/api/albums/{albumId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/albums/{albumId}")).StatusCode);

        await BlockAsync(ownerId, friendId);

        Assert.Equal(HttpStatusCode.NotFound, (await friend.GetAsync($"/api/albums/{albumId}")).StatusCode);
    }

    [Fact]
    public async Task Foreign_images_are_rejected_and_album_references_prevent_media_deletion()
    {
        var ownerId = await CreateUserAsync();
        var otherId = await CreateUserAsync();
        var albumId = await SeedAlbumAsync(ownerId, PhotoAlbumPrivacy.ONLY_ME);
        var foreignMediaId = await SeedReadyImageAsync(otherId);
        var ownedMediaId = await SeedReadyImageAsync(ownerId);
        using var owner = CreateAuthenticatedClient(ownerId);

        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(
            $"/api/albums/{albumId}/media", new { mediaId = foreignMediaId })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync(
            $"/api/albums/{albumId}/media", new { mediaId = ownedMediaId })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync($"/api/media/{ownedMediaId}")).StatusCode);
    }

    [Fact]
    public async Task Profile_standard_post_image_is_added_to_timeline_photos()
    {
        var ownerId = await CreateUserAsync();
        var mediaId = await SeedReadyImageAsync(ownerId);
        using var owner = CreateAuthenticatedClient(ownerId);

        var response = await owner.PostAsJsonAsync("/api/posts", new { content = "timeline photo", privacy = "public", mediaIds = new[] { mediaId } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var album = await db.PhotoAlbums.SingleAsync(item => item.OwnerUserId == ownerId && item.AlbumType == PhotoAlbumType.TIMELINE_PHOTOS);
        Assert.True(await db.AlbumMedia.AnyAsync(item => item.AlbumId == album.Id && item.MediaId == mediaId));
    }

    private async Task<Guid> CreateUserAsync()
    {
        var id = Guid.NewGuid();
        var username = "photos_" + id.ToString("N")[..16];
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.Users.Add(new User(id, $"{username}@example.com", username, DateTimeOffset.UtcNow));
        db.UserProfiles.Add(new UserProfile(id, username, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> SeedReadyImageAsync(Guid ownerId)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var media = new MediaAsset(
            id,
            ownerId,
            MediaType.IMAGE,
            $"{ownerId:N}/{id:N}.png",
            "profile.png",
            "image/png",
            32,
            now,
            now.AddMinutes(5));
        media.MarkReady(32, now);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.MediaAssets.Add(media);
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> SeedAlbumAsync(Guid ownerId, PhotoAlbumPrivacy privacy)
    {
        var id = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.PhotoAlbums.Add(new PhotoAlbum(id, ownerId, "Private photos", null, privacy, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return id;
    }

    private async Task AddFriendshipAsync(Guid firstUserId, Guid secondUserId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.Friendships.Add(new Friendship(firstUserId, secondUserId, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    private async Task BlockAsync(Guid blockerUserId, Guid blockedUserId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.BlockedUsers.Add(new BlockedUser(blockerUserId, blockedUserId, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    private HttpClient CreateAuthenticatedClient(Guid userId, bool allowAutoRedirect = true)
    {
        using var scope = factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            configuration["Jwt:Issuer"],
            configuration["Jwt:Audience"],
            [new(JwtRegisteredClaimNames.Sub, userId.ToString()), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())],
            now.AddSeconds(-1),
            now.AddMinutes(5),
            new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!)),
                SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = allowAutoRedirect });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
}
