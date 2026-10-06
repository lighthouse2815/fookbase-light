using Fookbase.Api.Modules.Groups.Domain.Enums;
using Fookbase.Api.Modules.Media.Domain.Enums;
using System.Data.Common;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Feed.Config;
using Fookbase.Api.Modules.Feed.DTOs.Responses;
using Fookbase.Api.Modules.Friends.Entities;
using Fookbase.Api.Modules.Friends.Services;
using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Pages.Domain.Enums;
using Fookbase.Api.Modules.Pages.Entities;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Reels.Entities;
using Fookbase.Api.Modules.Users.Domain.Enums;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class MixedFeedEndpointsTests(MixedFeedApiFactory factory) : IClassFixture<MixedFeedApiFactory>
{
    [Fact]
    public async Task Feed_reel_does_not_expose_email_from_legacy_profile_username()
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var userId = Guid.NewGuid();
        var email = $"legacy-{Guid.NewGuid():N}"[..20] + "@x.co";
        await SaveAsync(db =>
        {
            db.Users.Add(new User(userId, email, email, now));
            db.UserProfiles.Add(new UserProfile(userId, email, "dang", new DateOnly(2000, 1, 1), Gender.PREFER_NOT_TO_SAY, now));
        });
        var reel = await CreateReelAsync(userId, now, PostPrivacy.ONLY_ME);
        using var client = CreateClient(userId);

        var response = await client.GetAsync("/api/feed?limit=20");
        var raw = await response.Content.ReadAsStringAsync();
        var feed = await ReadAsync(response);
        var item = Assert.Single(feed.Items, candidate => candidate.Id == reel.Id);

        Assert.Empty(item.Author.Username);
        Assert.Empty(item.DisplayAuthor.Username);
        Assert.Equal("dang", item.DisplayAuthor.Name);
        Assert.DoesNotContain(email, raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Mixed_candidates_preserve_container_identity_privacy_and_page_publisher_confidentiality()
    {
        var users = await CreateUsersAsync(5);
        var (viewer, friend, stranger, manager, groupOwner) = (users[0], users[1], users[2], users[3], users[4]);
        await BefriendAsync(viewer, friend);
        var joined = await CreateGroupAsync(groupOwner, viewer, GroupPrivacy.PRIVATE);
        var unjoined = await CreateGroupAsync(groupOwner, null, GroupPrivacy.PUBLIC);
        var privateUnjoined = await CreateGroupAsync(groupOwner, null, GroupPrivacy.PRIVATE);
        var followed = await CreatePageAsync(manager, viewer);
        var unpublished = await CreatePageAsync(manager, viewer, published: false);
        var unfollowed = await CreatePageAsync(manager, null);
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var own = Standard(viewer, now, PostPrivacy.ONLY_ME);
        var friendPost = Standard(friend, now, PostPrivacy.FRIENDS);
        var nonFriendPost = Standard(stranger, now);
        var privateFriendPost = Standard(friend, now, PostPrivacy.ONLY_ME);
        var groupPost = InContainer(groupOwner, joined.Id, PostContainerType.GROUP, now);
        var unjoinedPost = InContainer(groupOwner, unjoined.Id, PostContainerType.GROUP, now);
        var hiddenGroupPost = InContainer(groupOwner, privateUnjoined.Id, PostContainerType.GROUP, now);
        var pagePost = InContainer(manager, followed.Id, PostContainerType.PAGE, now);
        var unpublishedPost = InContainer(manager, unpublished.Id, PostContainerType.PAGE, now);
        var unfollowedPost = InContainer(manager, unfollowed.Id, PostContainerType.PAGE, now);
        await SaveAsync(db => db.Posts.AddRange(own, friendPost, nonFriendPost, privateFriendPost, groupPost,
            unjoinedPost, hiddenGroupPost, pagePost, unpublishedPost, unfollowedPost));
        var ownReel = await CreateReelAsync(viewer, now, PostPrivacy.ONLY_ME);
        var friendReel = await CreateReelAsync(friend, now, PostPrivacy.FRIENDS);
        await BlockAsync(viewer, manager);
        using var client = CreateClient(viewer);

        var response = await client.GetAsync("/api/feed?limit=50");
        var raw = await response.Content.ReadAsStringAsync();
        var feed = await ReadAsync(response);
        var expected = new[] { own.Id, friendPost.Id, groupPost.Id, pagePost.Id, ownReel.Id, friendReel.Id };
        Assert.All(expected, id => Assert.Contains(feed.Items, item => item.Id == id));
        var excluded = new[] { nonFriendPost.Id, privateFriendPost.Id, unjoinedPost.Id, hiddenGroupPost.Id,
            unpublishedPost.Id, unfollowedPost.Id };
        Assert.All(excluded, id => Assert.DoesNotContain(feed.Items, item => item.Id == id));

        var groupItem = Assert.Single(feed.Items, item => item.Id == groupPost.Id);
        Assert.Equal("group", groupItem.ContainerType);
        Assert.Equal(joined.Id, groupItem.Container.Id);
        Assert.Equal(joined.Name, groupItem.Container.Name);
        Assert.Equal(groupOwner, groupItem.DisplayAuthor.Id);
        Assert.Equal(groupOwner, groupItem.Author.UserId);
        var pageItem = Assert.Single(feed.Items, item => item.Id == pagePost.Id);
        Assert.Equal("page", pageItem.DisplayAuthor.Type);
        Assert.Equal(followed.Id, pageItem.DisplayAuthor.Id);
        Assert.Equal(followed.Name, pageItem.Author.DisplayName);
        Assert.Null(pageItem.Author.UserId);
        Assert.DoesNotContain(manager.ToString(), raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(privateUnjoined.Name, raw, StringComparison.Ordinal);
        Assert.Equal("standardPost", Assert.Single(feed.Items, item => item.Id == own.Id).ContentType);

        var reelItem = Assert.Single(feed.Items, item => item.Id == friendReel.Id);
        Assert.Equal("reel", reelItem.ContentType);
        Assert.NotNull(reelItem.Video);
        Assert.Equal($"/api/reels/{friendReel.Id}/video/access", reelItem.Video.VideoAccessPath);
        Assert.Equal($"/api/reels/{friendReel.Id}/poster/access", reelItem.Video.PosterAccessPath);
        Assert.Equal(720, reelItem.Video.Width);
        Assert.Equal(1_280, reelItem.Video.Height);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/posts/{pagePost.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/posts/{hiddenGroupPost.Id}")).StatusCode);
    }

    [Fact]
    public async Task Reels_require_accessible_privacy_and_processed_ready_media_even_for_the_owner()
    {
        var users = await CreateUsersAsync(4);
        var (viewer, friend, stranger, blocked) = (users[0], users[1], users[2], users[3]);
        await BefriendAsync(viewer, friend);
        await BefriendAsync(viewer, blocked);
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        await SaveAsync(db => db.Posts.AddRange(Enumerable.Range(0, 12).Select(i => Standard(viewer, now.AddSeconds(-i)))));
        var ready = await CreateReelAsync(friend, now, PostPrivacy.FRIENDS);
        var hidden = await CreateReelAsync(friend, now, PostPrivacy.ONLY_ME);
        var strangersFriends = await CreateReelAsync(stranger, now, PostPrivacy.FRIENDS);
        var blockedPublic = await CreateReelAsync(blocked, now);
        var pending = await CreateReelAsync(viewer, now, status: MediaStatus.PENDING_UPLOAD);
        var processing = await CreateReelAsync(viewer, now, status: MediaStatus.PROCESSING);
        var failed = await CreateReelAsync(viewer, now, status: MediaStatus.FAILED);
        var missing = new Post(Guid.NewGuid(), viewer, "Missing media", PostPrivacy.ONLY_ME, now, postType: PostType.REEL);
        await SaveAsync(db => db.Posts.Add(missing));
        await BlockAsync(blocked, viewer);
        using var client = CreateClient(viewer);

        var feed = await ReadAsync(await client.GetAsync("/api/feed?limit=50"));

        Assert.Contains(feed.Items, item => item.Id == ready.Id);
        Assert.All(new[] { hidden.Id, strangersFriends.Id, blockedPublic.Id, pending.Id, processing.Id, failed.Id, missing.Id },
            id => Assert.DoesNotContain(feed.Items, item => item.Id == id));
        Assert.All(feed.Items.Where(item => item.ContentType == "reel"), item =>
        {
            Assert.NotNull(item.Video);
            Assert.True(item.Video.DurationMs > 0);
            Assert.DoesNotContain("http", item.Video.VideoAccessPath, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData("block")]
    [InlineData("reverse-block")]
    [InlineData("unfriend")]
    [InlineData("leave")]
    [InlineData("remove")]
    [InlineData("unfollow")]
    [InlineData("unpublish")]
    [InlineData("delete-group")]
    [InlineData("delete-post")]
    [InlineData("delete-reel")]
    public async Task Relationship_and_deletion_changes_revoke_content_even_with_an_old_cursor(string change)
    {
        var users = await CreateUsersAsync(3);
        var (viewer, friend, owner) = (users[0], users[1], users[2]);
        await BefriendAsync(viewer, friend);
        var group = await CreateGroupAsync(owner, viewer, GroupPrivacy.PRIVATE);
        var page = await CreatePageAsync(owner, viewer);
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var anchor = Standard(viewer, now);
        var friendPost = Standard(friend, now.AddMinutes(-1), PostPrivacy.FRIENDS);
        var groupPost = InContainer(owner, group.Id, PostContainerType.GROUP, now.AddMinutes(-2));
        var pagePost = InContainer(owner, page.Id, PostContainerType.PAGE, now.AddMinutes(-3));
        await SaveAsync(db => db.Posts.AddRange(anchor, friendPost, groupPost, pagePost));
        var reel = await CreateReelAsync(friend, now.AddMinutes(-4), PostPrivacy.FRIENDS);
        var target = change switch
        {
            "leave" or "remove" or "delete-group" => groupPost.Id,
            "unfollow" or "unpublish" => pagePost.Id,
            "delete-reel" => reel.Id,
            _ => friendPost.Id
        };
        using var client = CreateClient(viewer);
        var before = await ReadAsync(await client.GetAsync("/api/feed/following?limit=50"));
        Assert.Contains(before.Items, item => item.Id == target);
        var first = await ReadAsync(await client.GetAsync("/api/feed?limit=1"));
        Assert.Equal(anchor.Id, Assert.Single(first.Items).Id);
        Assert.NotNull(first.NextCursor);

        if (change is "block" or "reverse-block")
        {
            await BlockAsync(change == "block" ? viewer : friend, change == "block" ? friend : viewer);
        }
        else if (change is "leave" or "remove")
        {
            using var actor = CreateClient(change == "leave" ? viewer : owner);
            using var mutation = change == "leave"
                ? await actor.PostAsync($"/api/groups/{group.Id}/leave", null)
                : await actor.DeleteAsync($"/api/groups/{group.Id}/members/{viewer}");
            mutation.EnsureSuccessStatusCode();
        }
        else
        {
            await SaveAsync(db =>
            {
                switch (change)
                {
                    case "unfriend":
                        db.Friendships.Remove(db.Friendships.Single(item =>
                            (item.User1Id == viewer && item.User2Id == friend) ||
                            (item.User1Id == friend && item.User2Id == viewer)));
                        break;
                    case "unfollow":
                        db.PageFollowers.Remove(db.PageFollowers.Single(item => item.PageId == page.Id && item.UserId == viewer));
                        break;
                    case "unpublish":
                        db.Pages.Single(item => item.Id == page.Id).Unpublish(DateTimeOffset.UtcNow);
                        break;
                    case "delete-group":
                        db.Groups.Single(item => item.Id == group.Id).Delete(DateTimeOffset.UtcNow);
                        break;
                    default:
                        db.Posts.Single(item => item.Id == target).Delete(DateTimeOffset.UtcNow);
                        break;
                }
            });
        }

        if (change == "unfriend")
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            Assert.True(await db.UserFollows.AnyAsync(follow =>
                follow.FollowerUserId == viewer && follow.FollowingUserId == friend));
        }

        var remaining = await TraverseAsync(client, "/api/feed", 2, first.NextCursor);
        var fresh = await ReadAsync(await client.GetAsync("/api/feed?limit=50"));
        Assert.DoesNotContain(remaining, item => item.Id == target);
        Assert.DoesNotContain(fresh.Items, item => item.Id == target);
    }

    [Fact]
    public async Task Feed_session_excludes_new_content_until_refresh_and_ignores_engagement_changes_for_ordering()
    {
        var viewer = (await CreateUsersAsync(1))[0];
        var timestamp = DateTimeOffset.UtcNow.AddHours(-1);
        var posts = Enumerable.Range(0, 6).Select(i => Standard(viewer, timestamp.AddSeconds(-i))).ToArray();
        await SaveAsync(db => db.Posts.AddRange(posts));
        using var client = CreateClient(viewer);
        var started = DateTimeOffset.UtcNow;
        var first = await ReadAsync(await client.GetAsync("/api/feed?limit=2"));
        Assert.InRange(first.AsOfUtc, started.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.NotNull(first.NextCursor);
        var newer = Standard(viewer, DateTimeOffset.UtcNow);
        Assert.True(newer.CreatedAtUtc > first.AsOfUtc);
        await SaveAsync(db =>
        {
            db.Posts.Add(newer);
            db.PostReactions.Add(new PostReaction(posts[^1].Id, viewer, ReactionType.LOVE, DateTimeOffset.UtcNow));
            db.Comments.Add(new Comment(Guid.NewGuid(), posts[^1].Id, viewer, null, "New engagement", DateTimeOffset.UtcNow));
        });

        var second = await ReadAsync(await client.GetAsync(CursorUrl("/api/feed", 2, first.NextCursor)));
        Assert.Equal(first.AsOfUtc, second.AsOfUtc);
        var remaining = second.Items.Concat(await TraverseAsync(client, "/api/feed", 2, second.NextCursor)).ToArray();
        Assert.DoesNotContain(remaining, item => item.Id == newer.Id);
        Assert.Equal(posts.Select(post => post.Id), first.Items.Concat(remaining).Where(item => !item.IsSuggested).Select(item => item.Id));
        var reacted = Assert.Single(remaining, item => item.Id == posts[^1].Id);
        Assert.Equal(1, reacted.CommentCount);
        Assert.Equal(1, reacted.ReactionCount);
        Assert.Equal("love", reacted.ViewerReaction);
        var refreshed = await ReadAsync(await client.GetAsync("/api/feed?limit=2"));
        Assert.True(refreshed.AsOfUtc >= newer.CreatedAtUtc);
        Assert.Equal(newer.Id, refreshed.Items[0].Id);
    }

    [Fact]
    public async Task Organic_profile_candidates_require_follow_and_keep_friend_and_non_friend_privacy_distinct()
    {
        var users = await CreateUsersAsync(4);
        var (viewer, friend, followedNonFriend, stranger) = (users[0], users[1], users[2], users[3]);
        await BefriendAsync(viewer, friend);
        await FollowAsync(viewer, followedNonFriend);
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var friendPost = Standard(friend, now, PostPrivacy.FRIENDS);
        var followedPublic = Standard(followedNonFriend, now.AddSeconds(-1));
        var followedFriendsOnly = Standard(followedNonFriend, now.AddSeconds(-2), PostPrivacy.FRIENDS);
        var strangerPublic = Standard(stranger, now.AddSeconds(-3));
        await SaveAsync(db => db.Posts.AddRange(friendPost, followedPublic, followedFriendsOnly, strangerPublic));
        var followedReel = await CreateReelAsync(followedNonFriend, now.AddSeconds(-4));
        using var client = CreateClient(viewer);

        var feed = await ReadAsync(await client.GetAsync("/api/feed?limit=50"));

        Assert.All(new[] { friendPost.Id, followedPublic.Id, followedReel.Id }, id =>
            Assert.Contains(feed.Items, item => item.Id == id && !item.IsSuggested));
        Assert.All(new[] { followedFriendsOnly.Id, strangerPublic.Id }, id =>
            Assert.DoesNotContain(feed.Items, item => item.Id == id && !item.IsSuggested));

        await SaveAsync(db => db.UserFollows.Remove(db.UserFollows.Single(follow =>
            follow.FollowerUserId == viewer && follow.FollowingUserId == friend)));
        var refreshed = await ReadAsync(await client.GetAsync("/api/feed?limit=50"));

        Assert.DoesNotContain(refreshed.Items, item => item.Id == friendPost.Id);
        Assert.Contains(refreshed.Items, item => item.Id == followedPublic.Id && !item.IsSuggested);
    }

    [Fact]
    public async Task Unfollowing_friend_removes_organic_reels_but_allows_public_reel_discovery()
    {
        var users = await CreateUsersAsync(2);
        var (viewer, friend) = (users[0], users[1]);
        await BefriendAsync(viewer, friend);
        var now = DateTimeOffset.UtcNow;
        var ownPosts = Enumerable.Range(0, 4).Select(i => Standard(viewer, now.AddSeconds(-i - 2))).ToArray();
        await SaveAsync(db => db.Posts.AddRange(ownPosts));
        var publicReel = await CreateReelAsync(friend, now);
        var friendsReel = await CreateReelAsync(friend, now.AddSeconds(-1), PostPrivacy.FRIENDS);
        using var client = CreateClient(viewer);

        var beforeUnfollow = await ReadAsync(await client.GetAsync("/api/feed?limit=50"));
        Assert.All(new[] { publicReel.Id, friendsReel.Id }, id =>
            Assert.Contains(beforeUnfollow.Items, item => item.Id == id && !item.IsSuggested));

        await SaveAsync(db => db.UserFollows.Remove(db.UserFollows.Single(follow =>
            follow.FollowerUserId == viewer && follow.FollowingUserId == friend)));
        var refreshed = await ReadAsync(await client.GetAsync("/api/feed?limit=50"));

        Assert.Equal(ownPosts.Select(post => post.Id), refreshed.Items.Where(item => !item.IsSuggested).Select(item => item.Id));
        Assert.DoesNotContain(refreshed.Items, item => item.Id == publicReel.Id && !item.IsSuggested);
        Assert.DoesNotContain(refreshed.Items, item => item.Id == friendsReel.Id);
        Assert.DoesNotContain(refreshed.Items, item => item.Id == friendsReel.Id);
        Assert.All(refreshed.Items.Where(item => item.IsSuggested), item =>
            Assert.True(item.ContentType is "reel" or "standardPost" && item.Privacy == "public"));
    }

    [Fact]
    public async Task Old_cursor_rechecks_follow_eligibility_before_returning_profile_content()
    {
        var users = await CreateUsersAsync(2);
        var (viewer, friend) = (users[0], users[1]);
        await BefriendAsync(viewer, friend);
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var own = Standard(viewer, now);
        var friendPost = Standard(friend, now.AddSeconds(-1), PostPrivacy.FRIENDS);
        await SaveAsync(db => db.Posts.AddRange(own, friendPost));
        using var client = CreateClient(viewer);

        var first = await ReadAsync(await client.GetAsync("/api/feed?limit=1"));
        Assert.Equal(own.Id, Assert.Single(first.Items).Id);
        Assert.NotNull(first.NextCursor);

        await SaveAsync(db => db.UserFollows.Remove(db.UserFollows.Single(follow =>
            follow.FollowerUserId == viewer && follow.FollowingUserId == friend)));
        var remaining = await TraverseAsync(client, "/api/feed", 1, first.NextCursor);

        Assert.DoesNotContain(remaining, item => item.Id == friendPost.Id);
    }

    [Fact]
    public async Task Following_mode_uses_follows_and_never_inserts_suggested_reels()
    {
        var users = await CreateUsersAsync(3);
        var (viewer, followed, stranger) = (users[0], users[1], users[2]);
        await FollowAsync(viewer, followed);
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var followedPost = Standard(followed, now);
        var strangerPost = Standard(stranger, now.AddSeconds(-1));
        await SaveAsync(db => db.Posts.AddRange(followedPost, strangerPost));
        var followedReel = await CreateReelAsync(followed, now.AddSeconds(-2));
        var strangerReel = await CreateReelAsync(stranger, now.AddSeconds(-3));
        using var client = CreateClient(viewer);

        var following = await TraverseAsync(client, "/api/feed/following", 20);

        Assert.All(following, item => Assert.False(item.IsSuggested));
        Assert.All(new[] { followedPost.Id, followedReel.Id }, id => Assert.Contains(following, item => item.Id == id));
        Assert.All(new[] { strangerPost.Id, strangerReel.Id }, id => Assert.DoesNotContain(following, item => item.Id == id));
    }

    [Fact]
    public async Task Cursor_is_authenticated_and_bound_to_viewer_and_feed_mode()
    {
        var users = await CreateUsersAsync(2);
        var viewer = users[0];
        await SaveAsync(db => db.Posts.AddRange(Enumerable.Range(0, 4)
            .Select(i => Standard(viewer, DateTimeOffset.UtcNow.AddMinutes(-i - 1)))));
        using var anonymous = factory.CreateClient();
        using var client = CreateClient(viewer);
        using var other = CreateClient(users[1]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/feed")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/feed/following")).StatusCode);
        var first = await ReadAsync(await client.GetAsync("/api/feed?limit=1"));
        var cursor = Assert.IsType<string>(first.NextCursor);
        var middle = cursor.Length / 2;
        var tampered = cursor[..middle] + (cursor[middle] == 'A' ? 'B' : 'A') + cursor[(middle + 1)..];
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(CursorUrl("/api/feed", 1, tampered))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.GetAsync(CursorUrl("/api/feed", 1, cursor))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(CursorUrl("/api/feed/following", 1, cursor))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/feed?cursor=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/feed?limit=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/feed?limit=51")).StatusCode);
        var chronological = await ReadAsync(await client.GetAsync("/api/feed/following?limit=1"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(CursorUrl("/api/feed", 1, chronological.NextCursor))).StatusCode);
    }

    [Fact]
    public async Task Equal_rank_and_timestamp_ties_use_id_and_remain_stable_across_page_sizes()
    {
        var viewer = (await CreateUsersAsync(1))[0];
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var posts = Enumerable.Range(0, 17).Select(_ => Standard(viewer, now)).ToArray();
        await SaveAsync(db => db.Posts.AddRange(posts));
        using var client = CreateClient(viewer);

        var smallPages = await TraverseAsync(client, "/api/feed", 3);
        var largePages = await TraverseAsync(client, "/api/feed", 11);
        var expected = posts.OrderByDescending(item => item.Id).Select(item => item.Id).ToArray();

        Assert.Equal(expected, smallPages.Where(item => !item.IsSuggested).Select(item => item.Id));
        Assert.Equal(expected, largePages.Where(item => !item.IsSuggested).Select(item => item.Id));
        Assert.Equal(smallPages.Count, smallPages.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public async Task Affinity_and_freshness_are_predictable_and_changed_options_invalidate_old_cursors()
    {
        var users = await CreateUsersAsync(4);
        var (viewer, friend, followedNonFriend, owner) = (users[0], users[1], users[2], users[3]);
        await BefriendAsync(viewer, friend);
        await FollowAsync(viewer, followedNonFriend);
        var group = await CreateGroupAsync(owner, viewer);
        var page = await CreatePageAsync(owner, viewer);
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var own = Standard(viewer, now);
        var friendPost = Standard(friend, now);
        var followedNonFriendPost = Standard(followedNonFriend, now);
        var groupPost = InContainer(owner, group.Id, PostContainerType.GROUP, now);
        var pagePost = InContainer(owner, page.Id, PostContainerType.PAGE, now);
        var oldOwn = Standard(viewer, now.AddHours(-48));
        await SaveAsync(db => db.Posts.AddRange(own, friendPost, followedNonFriendPost, groupPost, pagePost, oldOwn));
        using var client = CreateClient(viewer);

        var normal = await TraverseAsync(client, "/api/feed", 2);
        Assert.Equal(new[] { own.Id, friendPost.Id, followedNonFriendPost.Id, groupPost.Id, pagePost.Id, oldOwn.Id },
            normal.Where(item => !item.IsSuggested).Select(item => item.Id));
        var first = await ReadAsync(await client.GetAsync("/api/feed?limit=1"));
        var sharedProtection = factory.Services.GetRequiredService<IDataProtectionProvider>();
        using var changedFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<FeedRankingOptions>();
            services.AddSingleton(new FeedRankingOptions
            {
                OwnAffinity = 0,
                FriendAffinity = 30,
                FollowedNonFriendProfile = 15,
                GroupAffinity = 10,
                PageAffinity = 5,
                FreshnessHoursPerPoint = 1,
                CandidateLimitPerSource = 51
            });
            // Reuse protection keys so the failure below specifically exercises config binding.
            services.RemoveAll<IDataProtectionProvider>();
            services.AddSingleton(sharedProtection);
        }));
        using var changedClient = CreateClient(viewer, changedFactory);
        var changed = await TraverseAsync(changedClient, "/api/feed", 2);
        Assert.Equal(new[] { friendPost.Id, followedNonFriendPost.Id, groupPost.Id, pagePost.Id, own.Id, oldOwn.Id },
            changed.Where(item => !item.IsSuggested).Select(item => item.Id));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await changedClient.GetAsync(CursorUrl("/api/feed", 1, first.NextCursor))).StatusCode);
    }

    [Fact]
    public async Task Recent_creator_interactions_rank_a_followed_creator_ahead_of_a_comparable_creator()
    {
        var users = await CreateUsersAsync(3);
        var (viewer, bob, carol) = (users[0], users[1], users[2]);
        await FollowAsync(viewer, bob);
        await FollowAsync(viewer, carol);
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var bobPost = Standard(bob, now);
        var carolPost = Standard(carol, now);
        await SaveAsync(db =>
        {
            db.Posts.AddRange(bobPost, carolPost);
            db.PostReactions.Add(new PostReaction(bobPost.Id, viewer, ReactionType.LOVE, now));
            db.Comments.AddRange(
                new Comment(Guid.NewGuid(), bobPost.Id, viewer, null, "Great post", now),
                new Comment(Guid.NewGuid(), bobPost.Id, viewer, null, "Following along", now));
        });
        using var client = CreateClient(viewer);

        var feed = await ReadAsync(await client.GetAsync("/api/feed?limit=10"));

        Assert.Equal(bobPost.Id, feed.Items.First(item => !item.IsSuggested).Id);
        Assert.Contains(feed.Items, item => item.Id == carolPost.Id);
    }

    [Fact]
    public async Task Group_and_page_interactions_raise_their_eligible_source_content()
    {
        var users = await CreateUsersAsync(2);
        var (viewer, owner) = (users[0], users[1]);
        var group = await CreateGroupAsync(owner, viewer);
        var otherGroup = await CreateGroupAsync(owner, viewer);
        var page = await CreatePageAsync(owner, viewer);
        var otherPage = await CreatePageAsync(owner, viewer);
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var preferredGroup = InContainer(owner, group.Id, PostContainerType.GROUP, now);
        var otherGroupPost = InContainer(owner, otherGroup.Id, PostContainerType.GROUP, now);
        var preferredPage = InContainer(owner, page.Id, PostContainerType.PAGE, now);
        var otherPagePost = InContainer(owner, otherPage.Id, PostContainerType.PAGE, now);
        await SaveAsync(db =>
        {
            db.Posts.AddRange(preferredGroup, otherGroupPost, preferredPage, otherPagePost);
            db.Comments.Add(new Comment(Guid.NewGuid(), preferredGroup.Id, viewer, null, "Useful group", now));
            db.PostReactions.Add(new PostReaction(preferredPage.Id, viewer, ReactionType.LIKE, now));
        });
        using var client = CreateClient(viewer);

        var feed = await ReadAsync(await client.GetAsync("/api/feed?limit=10"));
        var organic = feed.Items.Where(item => !item.IsSuggested).Select(item => item.Id).ToList();

        Assert.True(organic.IndexOf(preferredGroup.Id) < organic.IndexOf(otherGroupPost.Id));
        Assert.True(organic.IndexOf(preferredPage.Id) < organic.IndexOf(otherPagePost.Id));
    }

    [Fact]
    public async Task Strong_recent_reel_completion_ranks_the_creator_reel_higher()
    {
        var users = await CreateUsersAsync(3);
        var (viewer, bob, carol) = (users[0], users[1], users[2]);
        await FollowAsync(viewer, bob);
        await FollowAsync(viewer, carol);
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var bobReel = await CreateReelAsync(bob, now);
        var carolReel = await CreateReelAsync(carol, now);
        await SaveAsync(db => db.ReelViews.Add(new ReelView(Guid.NewGuid(), bobReel.Id, viewer, 2_000, true, false, now)));
        using var client = CreateClient(viewer);

        var feed = await ReadAsync(await client.GetAsync("/api/feed?limit=10"));

        Assert.Equal(bobReel.Id, feed.Items.First(item => !item.IsSuggested).Id);
        Assert.Contains(feed.Items, item => item.Id == carolReel.Id);
    }

    [Fact]
    public async Task Suggested_items_are_labeled_and_do_not_repeat_the_same_creator_consecutively()
    {
        var users = await CreateUsersAsync(3);
        var (viewer, firstCreator, secondCreator) = (users[0], users[1], users[2]);
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        await SaveAsync(db => db.Posts.AddRange(Enumerable.Range(0, 12).Select(i => Standard(viewer, now.AddSeconds(-i)))));
        await CreateReelAsync(firstCreator, now);
        await CreateReelAsync(secondCreator, now.AddSeconds(-1));
        using var client = CreateClient(viewer);

        var feed = await TraverseAsync(client, "/api/feed", 2);
        var suggestions = feed.Where(item => item.IsSuggested).ToArray();

        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, item => Assert.Equal("Suggested for you", item.RecommendationReason));
        Assert.All(suggestions.Zip(suggestions.Skip(1)), pair => Assert.NotEqual(pair.First.Author.UserId, pair.Second.Author.UserId));
    }

    [Fact]
    public async Task Ranking_affinity_never_overrides_a_new_block()
    {
        var users = await CreateUsersAsync(2);
        var (viewer, creator) = (users[0], users[1]);
        await FollowAsync(viewer, creator);
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var post = Standard(creator, now);
        await SaveAsync(db =>
        {
            db.Posts.Add(post);
            db.PostReactions.Add(new PostReaction(post.Id, viewer, ReactionType.LOVE, now));
            db.Comments.Add(new Comment(Guid.NewGuid(), post.Id, viewer, null, "Previously interested", now));
        });
        await BlockAsync(viewer, creator);
        using var client = CreateClient(viewer);

        var feed = await ReadAsync(await client.GetAsync("/api/feed?limit=10"));

        Assert.DoesNotContain(feed.Items, item => item.Id == post.Id);
    }

    [Fact]
    public async Task Suggested_public_reels_have_a_session_wide_quota_and_never_enter_following()
    {
        var users = await CreateUsersAsync(2);
        var (viewer, stranger) = (users[0], users[1]);
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var posts = Enumerable.Range(0, 21).Select(i => Standard(viewer, now.AddSeconds(-i))).ToArray();
        await SaveAsync(db => db.Posts.AddRange(posts));
        for (var i = 0; i < 15; i++)
        {
            await CreateReelAsync(stranger, now.AddSeconds(-i));
        }
        using var client = CreateClient(viewer);

        var ranked = await TraverseAsync(client, "/api/feed", 1);
        var following = await TraverseAsync(client, "/api/feed/following", 3);
        var organic = 0;
        var suggested = 0;
        foreach (var item in ranked)
        {
            if (item.IsSuggested)
            {
                suggested++;
                Assert.Contains(item.ContentType, new[] { "reel", "standardPost" });
                Assert.Equal("public", item.Privacy);
                Assert.Equal("Suggested for you", item.RecommendationReason);
                Assert.True(suggested <= organic / 4, "Discovery exceeded one suggestion per four organic items across cursors.");
            }
            else
            {
                organic++;
            }
        }
        Assert.Equal(posts.Length, organic);
        Assert.InRange(suggested, 1, posts.Length / 4);
        var varied = new List<FeedItemResponse>();
        string? cursor = null;
        foreach (var limit in new[] { 3, 1, 7, 2, 50 })
        {
            var page = await ReadAsync(await client.GetAsync(CursorUrl("/api/feed", limit, cursor)));
            varied.AddRange(page.Items);
            cursor = page.NextCursor;
            if (cursor is null)
                break;
        }
        Assert.Null(cursor);
        Assert.Equal(ranked.Select(item => item.Id), varied.Select(item => item.Id));
        Assert.All(following, item => Assert.False(item.IsSuggested));
        Assert.Equal(posts.Select(item => item.Id), following.Select(item => item.Id));
    }

    [Fact]
    public async Task Following_orders_all_organic_sources_chronologically_with_a_stable_snapshot()
    {
        var users = await CreateUsersAsync(3);
        var (viewer, friend, owner) = (users[0], users[1], users[2]);
        await BefriendAsync(viewer, friend);
        var group = await CreateGroupAsync(owner, viewer);
        var page = await CreatePageAsync(owner, viewer);
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var own = Standard(viewer, now.AddMinutes(-3));
        var friendPost = Standard(friend, now.AddMinutes(-2));
        var groupPost = InContainer(owner, group.Id, PostContainerType.GROUP, now.AddMinutes(-1));
        var pagePost = InContainer(owner, page.Id, PostContainerType.PAGE, now);
        await SaveAsync(db => db.Posts.AddRange(own, friendPost, groupPost, pagePost));
        var reel = await CreateReelAsync(friend, now.AddSeconds(-30), PostPrivacy.FRIENDS);
        using var client = CreateClient(viewer);
        var first = await ReadAsync(await client.GetAsync("/api/feed/following?limit=2"));
        var second = await ReadAsync(await client.GetAsync(CursorUrl("/api/feed/following", 2, first.NextCursor)));
        var last = await ReadAsync(await client.GetAsync(CursorUrl("/api/feed/following", 2, second.NextCursor)));

        Assert.Equal(first.AsOfUtc, second.AsOfUtc);
        Assert.Equal(first.AsOfUtc, last.AsOfUtc);
        Assert.Equal(new[] { pagePost.Id, reel.Id, groupPost.Id, friendPost.Id, own.Id },
            first.Items.Concat(second.Items).Concat(last.Items).Select(item => item.Id));
        Assert.Null(last.NextCursor);
    }

    [Fact]
    public async Task Mixed_history_crosses_candidate_windows_without_skips_or_per_item_sql_growth()
    {
        var users = await CreateUsersAsync(3);
        var (viewer, friend, owner) = (users[0], users[1], users[2]);
        await BefriendAsync(viewer, friend);
        var group = await CreateGroupAsync(owner, viewer, GroupPrivacy.PRIVATE);
        var page = await CreatePageAsync(owner, viewer);
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var expected = new List<Post>();
        await SaveAsync(db =>
        {
            // Each of the four organic source windows exceeds the default 100 rows.
            for (var i = 0; i < 122; i++)
            {
                var timestamp = now.AddMinutes(-i);
                expected.Add(Standard(viewer, timestamp, PostPrivacy.ONLY_ME));
                expected.Add(Standard(friend, timestamp, PostPrivacy.FRIENDS));
                expected.Add(InContainer(owner, group.Id, PostContainerType.GROUP, timestamp));
                expected.Add(InContainer(owner, page.Id, PostContainerType.PAGE, timestamp));
                var reel = new Post(Guid.NewGuid(), friend, "Mixed history Reel", PostPrivacy.FRIENDS, timestamp, postType: PostType.REEL);
                expected.Add(reel);
                AttachVideo(db, reel, MediaStatus.READY);
            }
            db.Posts.AddRange(expected);
        });
        using var client = CreateClient(viewer);
        await ReadAsync(await client.GetAsync("/api/feed?limit=5"));
        factory.Commands.Reset();
        var small = await ReadAsync(await client.GetAsync("/api/feed?limit=5"));
        var smallCount = factory.Commands.Count;
        factory.Commands.Reset();
        var watch = Stopwatch.StartNew();
        var large = await ReadAsync(await client.GetAsync("/api/feed?limit=50"));
        var largeCount = factory.Commands.Count;
        watch.Stop();

        Assert.Equal(5, small.Items.Count);
        Assert.Equal(50, large.Items.Count);
        // Ranking V2 adds a fixed interaction/engagement aggregate batch; the cap still guards against per-item SQL.
        Assert.InRange(smallCount, 1, 45);
        Assert.InRange(largeCount, 1, 45);
        Assert.True(largeCount <= smallCount + 6,
            $"SQL command count grew from {smallCount} for 5 items to {largeCount} for 50 items.");
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(15), $"A mixed Feed page took {watch.Elapsed}.");
        foreach (var endpoint in new[] { "/api/feed", "/api/feed/following" })
        {
            var items = await TraverseAsync(client, endpoint, 13);
            var organicIds = items.Where(item => !item.IsSuggested).Select(item => item.Id).ToArray();
            Assert.Equal(expected.Count, organicIds.Length);
            Assert.Equal(expected.Select(item => item.Id).Order(), organicIds.Order());
            Assert.Equal(items.Count, items.Select(item => item.Id).Distinct().Count());
        }
    }

    private async Task<Guid[]> CreateUsersAsync(int count)
    {
        var now = DateTimeOffset.UtcNow;
        var users = Enumerable.Range(0, count).Select(_ =>
        {
            var id = Guid.NewGuid();
            var username = "mixed_" + id.ToString("N")[..24];
            return new User(id, username + "@example.com", username, now);
        }).ToArray();
        await SaveAsync(db =>
        {
            db.Users.AddRange(users);
            db.UserProfiles.AddRange(users.Select(user => new UserProfile(user.Id, user.UserName!, now)));
        });
        return users.Select(user => user.Id).ToArray();
    }

    private Task BefriendAsync(Guid viewer, Guid friend) => SaveAsync(db =>
    {
        var now = DateTimeOffset.UtcNow;
        db.Friendships.Add(new Friendship(viewer, friend, now));
        db.UserFollows.AddRange(
            new UserFollow(viewer, friend, now),
            new UserFollow(friend, viewer, now));
    });

    private Task FollowAsync(Guid follower, Guid following) => SaveAsync(db =>
        db.UserFollows.Add(new UserFollow(follower, following, DateTimeOffset.UtcNow)));

    private async Task BlockAsync(Guid viewer, Guid other)
    {
        using var scope = factory.Services.CreateScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<FriendsService>().BlockAsync(viewer, other)).Succeeded);
    }

    private async Task<Group> CreateGroupAsync(Guid owner, Guid? viewer, GroupPrivacy privacy = GroupPrivacy.PUBLIC)
    {
        var now = DateTimeOffset.UtcNow;
        var group = new Group(Guid.NewGuid(), "Mixed Group " + Guid.NewGuid().ToString("N"), "Feed context", privacy, owner, now);
        await SaveAsync(db =>
        {
            db.Groups.Add(group);
            db.GroupMembers.Add(new GroupMember(group.Id, owner, GroupMemberRole.OWNER, now));
            if (viewer is not null)
                db.GroupMembers.Add(new GroupMember(group.Id, viewer.Value, GroupMemberRole.MEMBER, now));
        });
        return group;
    }

    private async Task<Page> CreatePageAsync(Guid owner, Guid? viewer, bool published = true)
    {
        var now = DateTimeOffset.UtcNow;
        var page = new Page(Guid.NewGuid(), "Mixed Page", "mixed_" + Guid.NewGuid().ToString("N"), "Community", null, owner, now);
        if (published)
            page.Publish(now);
        await SaveAsync(db =>
        {
            db.Pages.Add(page);
            db.PageMembers.Add(new PageMember(page.Id, owner, PageRole.OWNER, now));
            if (viewer is not null)
                db.PageFollowers.Add(new PageFollower(page.Id, viewer.Value, now));
        });
        return page;
    }

    private static Post Standard(Guid owner, DateTimeOffset createdAt, PostPrivacy privacy = PostPrivacy.PUBLIC) =>
        new Post(Guid.NewGuid(), owner, "Mixed profile post", privacy, createdAt);

    private static Post InContainer(Guid owner, Guid container, PostContainerType type, DateTimeOffset createdAt) =>
        new Post(Guid.NewGuid(), owner, "Mixed container post", PostPrivacy.PUBLIC, type, container, createdAt);

    private async Task<Post> CreateReelAsync(Guid owner, DateTimeOffset createdAt, PostPrivacy privacy = PostPrivacy.PUBLIC,
        MediaStatus status = MediaStatus.READY)
    {
        var reel = new Post(Guid.NewGuid(), owner, "Mixed Reel", privacy, createdAt, postType: PostType.REEL);
        await SaveAsync(db =>
        {
            db.Posts.Add(reel);
            AttachVideo(db, reel, status);
        });
        return reel;
    }

    private static void AttachVideo(FookbaseDbContext db, Post reel, MediaStatus status)
    {
        var id = Guid.NewGuid();
        var media = new MediaAsset(id, reel.AuthorUserId, MediaType.VIDEO, id + ".mp4", "reel.mp4", "video/mp4", 100,
            reel.CreatedAtUtc, DateTimeOffset.UtcNow.AddMinutes(5));
        if (status is MediaStatus.PROCESSING or MediaStatus.READY)
            media.MarkProcessing(100, reel.CreatedAtUtc);
        if (status == MediaStatus.READY)
            media.MarkVideoReady(id + "-processed.mp4", id + "-poster.jpg", 2_000, 720, 1_280, reel.CreatedAtUtc);
        if (status == MediaStatus.FAILED)
            media.MarkFailed();
        db.MediaAssets.Add(media);
        db.PostMedia.Add(new PostMedia(reel.Id, id, 0));
    }

    private async Task SaveAsync(Action<FookbaseDbContext> seed)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    private HttpClient CreateClient(Guid viewer, WebApplicationFactory<Program>? application = null)
    {
        application ??= factory;
        var configuration = application.Services.GetRequiredService<IConfiguration>();
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"],
            [new Claim(JwtRegisteredClaimNames.Sub, viewer.ToString()), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())],
            notBefore: now.AddSeconds(-1), expires: now.AddMinutes(15),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!)),
                SecurityAlgorithms.HmacSha256));
        var client = application.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private static string CursorUrl(string endpoint, int limit, string? cursor) =>
        endpoint + "?limit=" + limit + (cursor is null ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor));

    private static async Task<List<FeedItemResponse>> TraverseAsync(HttpClient client, string endpoint, int limit, string? cursor = null)
    {
        var items = new List<FeedItemResponse>();
        DateTimeOffset? snapshot = null;
        for (var page = 0; page < 150; page++)
        {
            var response = await ReadAsync(await client.GetAsync(CursorUrl(endpoint, limit, cursor)));
            snapshot ??= response.AsOfUtc;
            Assert.Equal(snapshot.Value, response.AsOfUtc);
            items.AddRange(response.Items);
            cursor = response.NextCursor;
            if (cursor is null)
                return items;
        }
        Assert.Fail("Feed did not terminate after 150 pages.");
        return items;
    }

    private static async Task<FeedPageResponse> ReadAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {raw}");
        return JsonSerializer.Deserialize<FeedPageResponse>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Feed response was empty.");
    }
}

public sealed class MixedFeedApiFactory : PostsApiFactory
{
    public FeedCommandCounter Commands { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
            services.ConfigureDbContext<FookbaseDbContext>(options => options.AddInterceptors(Commands)));
    }
}

public sealed class FeedCommandCounter : DbCommandInterceptor
{
    private int count;
    public int Count => Volatile.Read(ref count);
    public void Reset() => Interlocked.Exchange(ref count, 0);

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Interlocked.Increment(ref count);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref count);
        return ValueTask.FromResult(result);
    }
}
