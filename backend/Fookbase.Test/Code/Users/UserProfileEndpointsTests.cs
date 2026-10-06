using Fookbase.Api.Modules.Media.Domain.Enums;
using Fookbase.Api.Modules.Users.DTOs.Requests;
using Fookbase.Api.Modules.Users.DTOs.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Users.Domain.Enums;
using Fookbase.Api.Modules.Users.Entities;
using Fookbase.Api.Modules.Users.Services;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Photos.Domain.Enums;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Friends.Entities;
using Fookbase.Api.Modules.Identity.Entities;
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
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var profile = await dbContext.UserProfiles.SingleAsync(
            item => item.UserId == user.Id);

        Assert.Equal(user.Username, profile.Username);
        Assert.Equal(user.Username, profile.DisplayName);
        Assert.Null(profile.Bio);
        Assert.Equal(1, await dbContext.UserProfiles.CountAsync(
            item => item.UserId == user.Id));
    }

    [Fact]
    public async Task Profile_creation_rejects_a_missing_user()
    {
        var user = CreateUser();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<UserProfileService>();

        await Assert.ThrowsAsync<DbUpdateException>(() => service.EnsureCreatedAsync(user.Id, user.Username));

        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.False(await db.UserProfiles.AnyAsync(profile => profile.UserId == user.Id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Profile_media_rejects_a_missing_asset(bool isAvatar)
    {
        var user = CreateUser();
        await EnsureProfileAsync(user);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var profile = await db.UserProfiles.SingleAsync(item => item.UserId == user.Id);
        var missingMediaId = Guid.NewGuid();
        profile.Update(null, null, null, null,
            isAvatar ? missingMediaId : null, isAvatar ? null : missingMediaId, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        db.ChangeTracker.Clear();
        var persisted = await db.UserProfiles.SingleAsync(item => item.UserId == user.Id);
        Assert.Null(persisted.AvatarMediaId);
        Assert.Null(persisted.CoverMediaId);
    }

    [Fact]
    public async Task Profile_loads_user_and_media_and_cascades_when_user_is_deleted()
    {
        var user = CreateUser();
        await EnsureProfileAsync(user);
        var avatarId = await CreateReadyImageAsync(user.Id);
        var coverId = await CreateReadyImageAsync(user.Id);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var profile = await db.UserProfiles.SingleAsync(item => item.UserId == user.Id);
        profile.Update(null, null, null, null, avatarId, coverId, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loaded = await db.UserProfiles.AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.AvatarMedia)
            .Include(item => item.CoverMedia)
            .SingleAsync(item => item.UserId == user.Id);

        Assert.Equal(user.Id, loaded.User.Id);
        Assert.Same(loaded, loaded.User.Profile);
        Assert.Equal(avatarId, loaded.AvatarMedia!.Id);
        Assert.Equal(coverId, loaded.CoverMedia!.Id);

        await db.Users.Where(item => item.Id == user.Id).ExecuteDeleteAsync();

        Assert.False(await db.UserProfiles.AnyAsync(item => item.UserId == user.Id));
        Assert.Equal(2, await db.MediaAssets.CountAsync(item => item.Id == avatarId || item.Id == coverId));
    }

    [Fact]
    public async Task Profile_preserves_zero_gender_value_instead_of_database_default()
    {
        var user = CreateUser();
        var now = DateTimeOffset.UtcNow;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        db.Users.Add(new User(user.Id, $"{user.Username}@example.com", user.Username, now));
        db.UserProfiles.Add(new UserProfile(
            user.Id, user.Username, user.Username, new DateOnly(2000, 1, 2), Gender.FEMALE, now));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var profile = await db.UserProfiles.SingleAsync(item => item.UserId == user.Id);

        Assert.Equal(Gender.FEMALE, profile.Gender);
        Assert.Equal(BirthdayVisibility.ONLY_ME, profile.BirthdayVisibility);
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
    public async Task Profile_and_people_search_project_visible_follow_counts_and_viewer_states()
    {
        var viewer = CreateUser();
        var target = CreateUser();
        var blockedRelation = CreateUser();
        var viewerBlockedRelation = CreateUser();
        var now = DateTimeOffset.UtcNow;
        await EnsureProfileAsync(viewer);
        await EnsureProfileAsync(target);
        await EnsureProfileAsync(blockedRelation);
        await EnsureProfileAsync(viewerBlockedRelation);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.UserFollows.AddRange(
                new UserFollow(viewer.Id, target.Id, now),
                new UserFollow(target.Id, viewer.Id, now),
                new UserFollow(blockedRelation.Id, target.Id, now),
                new UserFollow(target.Id, blockedRelation.Id, now),
                new UserFollow(viewerBlockedRelation.Id, target.Id, now),
                new UserFollow(target.Id, viewerBlockedRelation.Id, now));
            db.Friendships.Add(new Friendship(viewer.Id, target.Id, now));
            db.BlockedUsers.AddRange(
                new BlockedUser(target.Id, blockedRelation.Id, now),
                new BlockedUser(viewer.Id, viewerBlockedRelation.Id, now));
            await db.SaveChangesAsync();
        }

        using var authenticated = CreateAuthenticatedClient(viewer.Id);
        using var anonymous = factory.CreateClient();
        var profile = await authenticated.GetFromJsonAsync<UserProfileResponse>($"/api/users/{target.Id}");
        var search = await authenticated.GetFromJsonAsync<PagedResponse<UserProfileResponse>>(
            $"/api/users/search?query={target.Username}&offset=0&limit=20");
        var anonymousProfile = await anonymous.GetFromJsonAsync<UserProfileResponse>($"/api/users/{target.Id}");
        var anonymousSearch = await anonymous.GetFromJsonAsync<PagedResponse<UserProfileResponse>>(
            $"/api/users/search?query={target.Username}&offset=0&limit=20");

        Assert.NotNull(profile);
        Assert.Equal(1, profile.FollowerCount);
        Assert.Equal(1, profile.FollowingCount);
        Assert.True(profile.IsFollowing);
        Assert.True(profile.IsFollowedBy);
        Assert.Equal("friends", profile.FriendshipState);
        Assert.NotNull(search);
        var searchedTarget = Assert.Single(search.Items);
        Assert.Equal(profile.FollowerCount, searchedTarget.FollowerCount);
        Assert.Equal(profile.FollowingCount, searchedTarget.FollowingCount);
        Assert.True(searchedTarget.IsFollowing);
        Assert.True(searchedTarget.IsFollowedBy);
        Assert.Equal("friends", searchedTarget.FriendshipState);
        Assert.NotNull(anonymousProfile);
        Assert.Null(anonymousProfile.IsFollowing);
        Assert.Null(anonymousProfile.IsFollowedBy);
        Assert.Null(anonymousProfile.FriendshipState);
        Assert.NotNull(anonymousSearch);
        var anonymouslySearchedTarget = Assert.Single(anonymousSearch.Items);
        Assert.Equal(2, anonymouslySearchedTarget.FollowerCount);
        Assert.Equal(2, anonymouslySearchedTarget.FollowingCount);
        Assert.Null(anonymouslySearchedTarget.IsFollowing);
        Assert.Null(anonymouslySearchedTarget.IsFollowedBy);
        Assert.Null(anonymouslySearchedTarget.FriendshipState);
    }

    [Fact]
    public async Task Profile_and_people_search_hide_profiles_in_a_blocked_relationship()
    {
        var viewer = CreateUser();
        var target = CreateUser();
        var now = DateTimeOffset.UtcNow;
        await EnsureProfileAsync(viewer);
        await EnsureProfileAsync(target);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.BlockedUsers.Add(new BlockedUser(target.Id, viewer.Id, now));
            await db.SaveChangesAsync();
        }

        using var client = CreateAuthenticatedClient(viewer.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/users/{target.Id}")).StatusCode);
        var search = await client.GetFromJsonAsync<PagedResponse<UserProfileResponse>>(
            $"/api/users/search?query={target.Username}&offset=0&limit=20");

        Assert.NotNull(search);
        Assert.DoesNotContain(search.Items, item => item.UserId == target.Id);
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
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var otherProfile = await dbContext.UserProfiles.AsNoTracking().SingleAsync(
            item => item.UserId == userB.Id);
        Assert.Equal(userB.Username, otherProfile.DisplayName);
        Assert.Null(otherProfile.Bio);
    }

    [Theory]
    [InlineData("DisplayName", 100, "Tên hiển thị phải có từ 1 đến 100 ký tự.")]
    [InlineData("Bio", 500, "Giới thiệu không được vượt quá 500 ký tự.")]
    [InlineData("CurrentCity", 100, "Thành phố hiện tại không được vượt quá 100 ký tự.")]
    [InlineData("Hometown", 100, "Quê quán không được vượt quá 100 ký tự.")]
    [InlineData("Workplace", 150, "Nơi làm việc không được vượt quá 150 ký tự.")]
    [InlineData("Education", 150, "Học vấn không được vượt quá 150 ký tự.")]
    [InlineData("Website", 2048, "Website không được vượt quá 2048 ký tự.")]
    public async Task Patch_me_rejects_overlong_fields_with_vietnamese_validation_errors(
        string field, int maximumLength, string message)
    {
        var user = CreateUser();
        await EnsureProfileAsync(user);
        using var client = CreateAuthenticatedClient(user.Id);
        var value = field == "Website"
            ? "https://example.com/" + new string('a', maximumLength)
            : new string('a', maximumLength + 1);

        using var response = await client.PatchAsJsonAsync("/api/users/me",
            new Dictionary<string, string> { [field] = value });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation_failed", document.RootElement.GetProperty("code").GetString());
        var error = Assert.Single(document.RootElement.GetProperty("errors").GetProperty(field).EnumerateArray());
        Assert.Equal(message, error.GetString());
        var profile = await client.GetFromJsonAsync<UserProfileResponse>("/api/users/me");
        Assert.Equal(user.Username, profile!.DisplayName);
        Assert.Null(profile.Website);
    }

    [Theory]
    [InlineData("DisplayName", "", "Tên hiển thị phải có từ 1 đến 100 ký tự.")]
    [InlineData("DisplayName", "   ", "Tên hiển thị phải có từ 1 đến 100 ký tự.")]
    [InlineData("Website", "ftp://example.com", "Website phải là địa chỉ HTTP hoặc HTTPS hợp lệ.")]
    [InlineData("Website", "example.com", "Website phải là địa chỉ HTTP hoặc HTTPS hợp lệ.")]
    [InlineData("Website", "javascript:alert(1)", "Website phải là địa chỉ HTTP hoặc HTTPS hợp lệ.")]
    public async Task Patch_me_rejects_invalid_optional_text(string field, string value, string message)
    {
        var user = CreateUser();
        await EnsureProfileAsync(user);
        using var client = CreateAuthenticatedClient(user.Id);

        using var response = await client.PatchAsJsonAsync("/api/users/me",
            new Dictionary<string, string> { [field] = value });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = Assert.Single(document.RootElement.GetProperty("errors").GetProperty(field).EnumerateArray());
        Assert.Equal(message, error.GetString());
    }

    [Fact]
    public async Task Patch_me_rejects_undefined_birthday_visibility()
    {
        var user = CreateUser();
        await EnsureProfileAsync(user);
        using var client = CreateAuthenticatedClient(user.Id);

        using var response = await client.PatchAsJsonAsync("/api/users/me", new { birthdayVisibility = 42 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = Assert.Single(document.RootElement.GetProperty("errors")
            .GetProperty("BirthdayVisibility").EnumerateArray());
        Assert.Equal("Quyền hiển thị ngày sinh không hợp lệ.", error.GetString());
    }

    [Fact]
    public async Task Patch_me_accepts_null_fields_and_trims_display_name_at_the_length_limit()
    {
        var user = CreateUser();
        await EnsureProfileAsync(user);
        using var client = CreateAuthenticatedClient(user.Id);
        var displayName = new string('a', 100);

        using var update = await client.PatchAsJsonAsync("/api/users/me",
            new UpdateUserProfileRequest($"  {displayName}  ", null, null, null));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using var unchanged = await client.PatchAsJsonAsync("/api/users/me",
            new UpdateUserProfileRequest(null, null, null, null));
        Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
        var profile = await unchanged.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.Equal(displayName, profile!.DisplayName);
    }

    [Theory]
    [InlineData("DefaultPostPrivacy", "Quyền riêng tư mặc định của bài viết không hợp lệ.")]
    [InlineData("FriendRequestPolicy", "Chính sách lời mời kết bạn không hợp lệ.")]
    [InlineData("FriendListVisibility", "Quyền hiển thị danh sách bạn bè không hợp lệ.")]
    [InlineData("FollowListVisibility", "Quyền hiển thị danh sách theo dõi không hợp lệ.")]
    public async Task Patch_privacy_rejects_unsupported_values_without_changing_settings(string field, string message)
    {
        var user = CreateUser();
        await EnsureProfileAsync(user);
        using var client = CreateAuthenticatedClient(user.Id);
        using var initialized = await client.GetAsync("/api/privacy");
        Assert.Equal(HttpStatusCode.OK, initialized.StatusCode);
        var original = await client.GetFromJsonAsync<UserPrivacySettingsResponse>("/api/privacy");

        foreach (var value in new[] { "unsupported", "42" })
        {
            using var response = await client.PatchAsJsonAsync("/api/privacy",
                new Dictionary<string, string> { [field] = value });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("validation_failed", document.RootElement.GetProperty("code").GetString());
            var error = Assert.Single(document.RootElement.GetProperty("errors").GetProperty(field).EnumerateArray());
            Assert.Equal(message, error.GetString());
            Assert.Equal(original, await client.GetFromJsonAsync<UserPrivacySettingsResponse>("/api/privacy"));
        }
    }

    [Theory]
    [InlineData("onlyMe", "friendsOfFriends")]
    [InlineData("ONLY_ME", "FRIENDS_OF_FRIENDS")]
    [InlineData("2", "1")]
    public async Task Patch_privacy_preserves_supported_enum_formats_and_optional_fields(
        string privacy, string friendRequestPolicy)
    {
        var user = CreateUser();
        await EnsureProfileAsync(user);
        using var client = CreateAuthenticatedClient(user.Id);

        using var response = await client.PatchAsJsonAsync("/api/privacy",
            new UpdatePrivacySettingsRequest(privacy, friendRequestPolicy, privacy, privacy));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var partial = await client.PatchAsJsonAsync("/api/privacy",
            new UpdatePrivacySettingsRequest(null, " ", "", null));
        Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
        var settings = await partial.Content.ReadFromJsonAsync<UserPrivacySettingsResponse>();
        Assert.Equal("onlyMe", settings!.DefaultPostPrivacy);
        Assert.Equal("friendsOfFriends", settings.FriendRequestPolicy);
        Assert.Equal("onlyMe", settings.FriendListVisibility);
        Assert.Equal("onlyMe", settings.FollowListVisibility);
    }

    [Fact]
    public async Task Patch_me_allows_an_empty_website_to_clear_the_optional_value()
    {
        var user = CreateUser();
        await EnsureProfileAsync(user);
        using var client = CreateAuthenticatedClient(user.Id);

        var setWebsite = await client.PatchAsJsonAsync(
            "/api/users/me",
            new UpdateUserProfileRequest(null, null, null, null, Website: "https://example.com"));
        Assert.Equal(HttpStatusCode.OK, setWebsite.StatusCode);

        var clearWebsite = await client.PatchAsJsonAsync(
            "/api/users/me",
            new UpdateUserProfileRequest(null, null, null, null, Website: ""));

        Assert.Equal(HttpStatusCode.OK, clearWebsite.StatusCode);
        var updated = await clearWebsite.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(updated);
        Assert.Null(updated!.Website);
    }

    [Fact]
    public async Task Profile_hides_full_birth_date_from_non_owner()
    {
        var owner = CreateUser();
        var friend = CreateUser();
        await EnsureProfileAsync(owner);
        await EnsureProfileAsync(friend);
        var now = DateTimeOffset.UtcNow;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.Friendships.Add(new Friendship(owner.Id, friend.Id, now));
            await db.SaveChangesAsync();
        }

        using var ownerClient = CreateAuthenticatedClient(owner.Id);
        var update = await ownerClient.PatchAsJsonAsync(
            "/api/users/me",
            new UpdateUserProfileRequest("Owner", null, new DateOnly(2000, 1, 2), null));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var ownerProfile = await ownerClient.GetFromJsonAsync<UserProfileResponse>("/api/users/me");
        using var friendClient = CreateAuthenticatedClient(friend.Id);
        var friendProfile = await friendClient.GetFromJsonAsync<UserProfileResponse>($"/api/users/{owner.Id}");

        Assert.NotNull(ownerProfile);
        Assert.Equal(new DateOnly(2000, 1, 2), ownerProfile.DateOfBirth);
        Assert.NotNull(friendProfile);
        Assert.Null(friendProfile.DateOfBirth);
    }

    [Fact]
    public async Task Birthday_today_includes_visible_friends_and_excludes_blocked_friends()
    {
        var actor = CreateUser();
        var visibleFriend = CreateUser();
        var blockedFriend = CreateUser();
        await EnsureProfileAsync(actor);
        await EnsureProfileAsync(visibleFriend);
        await EnsureProfileAsync(blockedFriend);
        var now = DateTimeOffset.UtcNow;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.Friendships.AddRange(
                new Friendship(actor.Id, visibleFriend.Id, now),
                new Friendship(actor.Id, blockedFriend.Id, now));
            db.BlockedUsers.Add(new BlockedUser(actor.Id, blockedFriend.Id, now));
            await db.SaveChangesAsync();
        }

        var birthday = DateOnly.FromDateTime(DateTime.Now);
        foreach (var friend in new[] { visibleFriend, blockedFriend })
        {
            using var friendClient = CreateAuthenticatedClient(friend.Id);
            var update = await friendClient.PatchAsJsonAsync("/api/users/me",
                new UpdateUserProfileRequest(null, null, new DateOnly(2000, birthday.Month, birthday.Day), null,
                    BirthdayVisibility: BirthdayVisibility.FRIENDS));
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        }

        using var actorClient = CreateAuthenticatedClient(actor.Id);
        var birthdays = await actorClient.GetFromJsonAsync<IReadOnlyList<BirthdayFriendResponse>>("/api/birthdays/today");

        Assert.NotNull(birthdays);
        Assert.Contains(birthdays, item => item.UserId == visibleFriend.Id);
        Assert.DoesNotContain(birthdays, item => item.UserId == blockedFriend.Id);
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
            var mediaDb = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            Assert.Contains(await mediaDb.ProfileMediaReferences.AsNoTracking().ToListAsync(), reference =>
                reference.UserId == user.Id &&
                reference.Slot == ProfileMediaSlot.AVATAR &&
                reference.MediaId == avatar);
            Assert.Contains(await mediaDb.ProfileMediaReferences.AsNoTracking().ToListAsync(), reference =>
                reference.UserId == user.Id &&
                reference.Slot == ProfileMediaSlot.COVER &&
                reference.MediaId == cover);
        }

        var replaceAvatar = await client.PatchAsJsonAsync(
            "/api/users/me",
            new UpdateUserProfileRequest(null, null, null, null, replacementAvatar));
        Assert.Equal(HttpStatusCode.OK, replaceAvatar.StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/media/{avatar}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/media/{cover}")).StatusCode);
        Guid profilePicturesAlbumId;
        Guid avatarUpdatePostId;
        using (var verification = factory.Services.CreateScope())
        {
            var db = verification.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            var profile = await db.UserProfiles.AsNoTracking()
                .SingleAsync(item => item.UserId == user.Id);
            Assert.Equal(replacementAvatar, profile.AvatarMediaId);
            Assert.Equal(cover, profile.CoverMediaId);
            profilePicturesAlbumId = await db.PhotoAlbums.AsNoTracking()
                .Where(item => item.OwnerUserId == user.Id && item.AlbumType == PhotoAlbumType.PROFILE_PICTURES)
                .Select(item => item.Id)
                .SingleAsync();
            Assert.True(await db.AlbumMedia.AsNoTracking()
                .AnyAsync(item => item.AlbumId == profilePicturesAlbumId && item.MediaId == avatar));
            avatarUpdatePostId = await db.PostMedia.AsNoTracking()
                .Where(item => item.MediaId == avatar)
                .Select(item => item.PostId)
                .SingleAsync();
        }

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/albums/{profilePicturesAlbumId}/media/{avatar}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/media/{avatar}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/posts/{avatarUpdatePostId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/media/{avatar}")).StatusCode);
    }

    [Fact]
    public async Task Changing_avatar_or_cover_creates_a_profile_media_post_once()
    {
        var user = CreateUser();
        await EnsureProfileAsync(user);
        var avatar = await CreateReadyImageAsync(user.Id);
        var cover = await CreateReadyImageAsync(user.Id);
        using var client = CreateAuthenticatedClient(user.Id);

        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync(
            "/api/users/me",
            new UpdateUserProfileRequest(null, null, null, null, avatar, cover))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync(
            "/api/users/me",
            new UpdateUserProfileRequest(null, null, null, null, avatar, cover))).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var posts = await db.Posts.AsNoTracking()
            .Where(post => post.AuthorUserId == user.Id)
            .ToListAsync();

        Assert.Collection(
            posts.OrderBy(post => post.Content),
            post =>
            {
                Assert.Equal("đã cập nhật ảnh bìa.", post.Content);
                Assert.Equal(PostPrivacy.PUBLIC, post.Privacy);
                Assert.True(db.PostMedia.Any(media => media.PostId == post.Id && media.MediaId == cover));
            },
            post =>
            {
                Assert.Equal("đã cập nhật ảnh đại diện.", post.Content);
                Assert.Equal(PostPrivacy.PUBLIC, post.Privacy);
                Assert.True(db.PostMedia.Any(media => media.PostId == post.Id && media.MediaId == avatar));
            });
    }

    [Fact]
    public async Task Avatar_and_cover_update_posts_can_change_audience_but_not_content_or_media()
    {
        var user = CreateUser();
        await EnsureProfileAsync(user);
        var avatar = await CreateReadyImageAsync(user.Id);
        var cover = await CreateReadyImageAsync(user.Id);
        using var client = CreateAuthenticatedClient(user.Id);

        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync(
            "/api/users/me",
            new UpdateUserProfileRequest(null, null, null, null, avatar, cover))).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var posts = await db.Posts.AsNoTracking()
            .Where(post => post.AuthorUserId == user.Id)
            .OrderBy(post => post.Content)
            .ToListAsync();

        foreach (var post in posts)
        {
            var mediaIds = await db.PostMedia.AsNoTracking()
                .Where(media => media.PostId == post.Id)
                .OrderBy(media => media.SortOrder)
                .Select(media => media.MediaId)
                .ToArrayAsync();

            var privacyResponse = await client.PutAsJsonAsync(
                $"/api/posts/{post.Id}", new { content = post.Content, privacy = "friends", mediaIds });
            Assert.Equal(HttpStatusCode.OK, privacyResponse.StatusCode);
            Assert.Equal(PostPrivacy.FRIENDS, await db.Posts.AsNoTracking()
                .Where(item => item.Id == post.Id)
                .Select(item => item.Privacy)
                .SingleAsync());

            var contentResponse = await client.PutAsJsonAsync(
                $"/api/posts/{post.Id}", new { content = "Không được sửa", privacy = "friends", mediaIds });
            Assert.Equal(HttpStatusCode.Conflict, contentResponse.StatusCode);

            var mediaResponse = await client.PutAsJsonAsync(
                $"/api/posts/{post.Id}", new { content = post.Content, privacy = "onlyMe", mediaIds = Array.Empty<Guid>() });
            Assert.Equal(HttpStatusCode.Conflict, mediaResponse.StatusCode);
        }
    }

    private async Task<bool> EnsureProfileAsync(UserSeed user)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<UserProfileService>();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        if (!await db.Users.AnyAsync(item => item.Id == user.Id))
        {
            db.Users.Add(new User(user.Id, $"{user.Username}@example.com", user.Username, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        var existed = await db.UserProfiles.AnyAsync(profile => profile.UserId == user.Id);
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
        var mediaDb = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var now = DateTimeOffset.UtcNow;
        var mediaId = Guid.NewGuid();
        var asset = new MediaAsset(
            mediaId,
            ownerUserId,
            MediaType.IMAGE,
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
