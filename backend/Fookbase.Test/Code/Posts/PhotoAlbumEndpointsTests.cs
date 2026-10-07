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
using Fookbase.Api.Modules.Photos.Services;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class PhotoAlbumEndpointsTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Fact]
    public async Task System_album_creation_preserves_owner_foreign_key_failure()
    {
        var missingOwnerId = Guid.NewGuid();
        var mediaId = await SeedReadyImageAsync(missingOwnerId);
        using var scope = factory.Services.CreateScope();
        var photos = scope.ServiceProvider.GetRequiredService<PhotosService>();

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            photos.AddSystemMediaAsync(missingOwnerId, PhotoAlbumType.TIMELINE_PHOTOS, mediaId));

        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgresException.SqlState);
        Assert.Equal("FK_PhotoAlbums_AspNetUsers_OwnerUserId", postgresException.ConstraintName);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("album")]
    [InlineData("media")]
    public async Task Photo_foreign_keys_reject_missing_principals(string missingPrincipal)
    {
        var ownerId = await CreateUserAsync();
        var mediaId = await SeedReadyImageAsync(ownerId);
        var albumId = await SeedAlbumAsync(ownerId, PhotoAlbumPrivacy.ONLY_ME);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        if (missingPrincipal == "owner")
        {
            db.PhotoAlbums.Add(new PhotoAlbum(Guid.NewGuid(), Guid.NewGuid(), "Orphan album", null,
                PhotoAlbumPrivacy.ONLY_ME, DateTimeOffset.UtcNow));
        }
        else
        {
            db.AlbumMedia.Add(new AlbumMedia(
                missingPrincipal == "album" ? Guid.NewGuid() : albumId,
                missingPrincipal == "media" ? Guid.NewGuid() : mediaId,
                0, DateTimeOffset.UtcNow));
        }

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Deleting_an_album_cascades_link_rows_and_preserves_media_assets()
    {
        var ownerId = await CreateUserAsync();
        var mediaId = await SeedReadyImageAsync(ownerId);
        var albumId = await SeedAlbumAsync(ownerId, PhotoAlbumPrivacy.ONLY_ME);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.AlbumMedia.Add(new AlbumMedia(albumId, mediaId, 0, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var album = await db.PhotoAlbums.Include(item => item.OwnerUser)
            .Include(item => item.MediaItems).ThenInclude(item => item.Media)
            .SingleAsync(item => item.Id == albumId);
        Assert.Equal(ownerId, album.OwnerUser.Id);
        Assert.Equal(mediaId, Assert.Single(album.MediaItems).Media.Id);
        db.ChangeTracker.Clear();

        Assert.Equal(1, await db.PhotoAlbums.Where(item => item.Id == albumId).ExecuteDeleteAsync());
        Assert.False(await db.AlbumMedia.AnyAsync(item => item.AlbumId == albumId));
        Assert.True(await db.MediaAssets.AnyAsync(item => item.Id == mediaId && item.DeletedAtUtc == null));
    }

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

    [Fact]
    public async Task Multiple_album_summaries_load_and_paginate_with_equal_timestamps()
    {
        var ownerId = await CreateUserAsync();
        var now = DateTimeOffset.UtcNow;
        var albums = Enumerable.Range(0, 3).Select(index =>
            new PhotoAlbum(Guid.NewGuid(), ownerId, $"Album {index}", null, PhotoAlbumPrivacy.PUBLIC, now)).ToArray();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.PhotoAlbums.AddRange(albums);
            await db.SaveChangesAsync();
        }
        var mediaId = await SeedReadyImageAsync(ownerId);
        using var client = CreateAuthenticatedClient(ownerId);
        (await client.PostAsJsonAsync($"/api/albums/{albums[0].Id}/media", new { mediaId })).EnsureSuccessStatusCode();

        var firstResponse = await client.GetAsync($"/api/users/{ownerId}/albums?limit=2");
        firstResponse.EnsureSuccessStatusCode();
        var first = (await firstResponse.Content.ReadFromJsonAsync<PhotoCursorPageResponse<PhotoAlbumSummaryResponse>>())!;
        Assert.Equal(2, first.Items.Count);
        Assert.NotNull(first.NextCursor);
        var secondResponse = await client.GetAsync($"/api/users/{ownerId}/albums?limit=2&cursor={Uri.EscapeDataString(first.NextCursor)}");
        secondResponse.EnsureSuccessStatusCode();
        var second = (await secondResponse.Content.ReadFromJsonAsync<PhotoCursorPageResponse<PhotoAlbumSummaryResponse>>())!;
        Assert.Single(second.Items);
        Assert.Null(second.NextCursor);
        var items = first.Items.Concat(second.Items).ToList();
        Assert.Equal(albums.Select(album => album.Id).OrderDescending(), items.Select(album => album.Id));
        var populated = Assert.Single(items, album => album.Id == albums[0].Id);
        Assert.Equal(1, populated.PhotoCount);
        Assert.Equal($"/api/albums/{albums[0].Id}/media/{mediaId}/access", populated.PreviewUrl);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Album_media_pages_return_every_photo_once(int limit)
    {
        var ownerId = await CreateUserAsync();
        var albumId = await SeedAlbumAsync(ownerId, PhotoAlbumPrivacy.PUBLIC);
        var mediaIds = new List<Guid>();
        using var client = CreateAuthenticatedClient(ownerId);
        for (var index = 0; index < 4; index++)
        {
            var mediaId = await SeedReadyImageAsync(ownerId);
            mediaIds.Add(mediaId);
            (await client.PostAsJsonAsync($"/api/albums/{albumId}/media", new { mediaId })).EnsureSuccessStatusCode();
        }

        var seen = new List<Guid>();
        string? cursor = null;
        for (var pageIndex = 0; pageIndex < mediaIds.Count; pageIndex++)
        {
            using var response = await client.GetAsync($"/api/albums/{albumId}/media?limit={limit}" +
                (cursor is null ? "" : $"&cursor={Uri.EscapeDataString(cursor)}"));
            response.EnsureSuccessStatusCode();
            var page = (await response.Content.ReadFromJsonAsync<PhotoCursorPageResponse<AlbumMediaResponse>>())!;
            seen.AddRange(page.Items.Select(item => item.MediaId));
            cursor = page.NextCursor;
            if (cursor is null) break;
        }
        Assert.Null(cursor);
        Assert.Equal(mediaIds, seen);
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
