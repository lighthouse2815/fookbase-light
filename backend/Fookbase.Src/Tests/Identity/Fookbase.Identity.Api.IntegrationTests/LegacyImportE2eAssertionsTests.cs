using Fookbase.Api.Modules.Friends.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Messages.Entities;
using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class LegacyImportE2eAssertionsTests
{
    private static readonly Guid UserOneId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid UserTwoId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid FriendshipId = Guid.Parse("00000000-0000-0000-0000-000000000011");
    private static readonly Guid AvatarMediaId = Guid.Parse("00000000-0000-0000-0000-000000000021");
    private static readonly Guid CoverMediaId = Guid.Parse("00000000-0000-0000-0000-000000000022");
    private static readonly Guid PostMediaId = Guid.Parse("00000000-0000-0000-0000-000000000023");
    private static readonly Guid PostId = Guid.Parse("00000000-0000-0000-0000-000000000031");
    private static readonly Guid CommentId = Guid.Parse("00000000-0000-0000-0000-000000000041");
    private static readonly Guid ConversationId = Guid.Parse("00000000-0000-0000-0000-000000000051");
    private static readonly Guid FirstMessageId = Guid.Parse("00000000-0000-0000-0000-000000000061");
    private static readonly Guid LastMessageId = Guid.Parse("00000000-0000-0000-0000-000000000062");
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset ReadAtUtc =
        new(2024, 1, 2, 3, 9, 5, TimeSpan.Zero);

    [Fact]
    public async Task Imported_legacy_data_is_usable_by_the_unified_context()
    {
        if (Environment.GetEnvironmentVariable("FOOKBASE_LEGACY_IMPORT_E2E") != "1")
        {
            return;
        }

        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__FookbaseDatabase")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__FookbaseDatabase is required for the legacy import E2E test.");
        await using var dbContext = new FookbaseDbContext(
            new DbContextOptionsBuilder<FookbaseDbContext>()
                .UseNpgsql(connectionString)
                .Options);

        var user = await dbContext.Users.SingleAsync(candidate => candidate.Id == UserOneId);
        Assert.Equal("$2a$11$legacy-password-hash-preserved", user.PasswordHash);
        Assert.Equal(CreatedAtUtc, user.CreatedAt);

        var profile = await dbContext.UserProfiles.SingleAsync(candidate => candidate.UserId == UserOneId);
        Assert.Equal(AvatarMediaId, profile.AvatarMediaId);
        Assert.Equal(CoverMediaId, profile.CoverMediaId);
        Assert.Equal(CreatedAtUtc, profile.CreatedAt);
        Assert.Equal(CreatedAtUtc, profile.UpdatedAt);
        Assert.Contains(
            await dbContext.ProfileMediaReferences.ToListAsync(),
            reference => reference.UserId == UserOneId &&
                reference.Slot == ProfileMediaSlot.Avatar &&
                reference.MediaId == AvatarMediaId);
        Assert.Contains(
            await dbContext.ProfileMediaReferences.ToListAsync(),
            reference => reference.UserId == UserOneId &&
                reference.Slot == ProfileMediaSlot.Cover &&
                reference.MediaId == CoverMediaId);

        var friendship = await dbContext.Friendships.SingleAsync(candidate => candidate.Id == FriendshipId);
        Assert.Equal(UserOneId, friendship.UserId1);
        Assert.Equal(UserTwoId, friendship.UserId2);
        Assert.Equal(CreatedAtUtc, friendship.CreatedAtUtc);
        Assert.Contains(
            await dbContext.BlockedUsers.ToListAsync(),
            block => block.BlockerUserId == UserTwoId &&
                block.BlockedUserId == UserOneId &&
                block.CreatedAtUtc == CreatedAtUtc);

        var post = await dbContext.Posts.SingleAsync(candidate => candidate.Id == PostId);
        Assert.Equal(UserOneId, post.AuthorUserId);
        Assert.Equal("legacy post", post.Content);
        Assert.Equal(CreatedAtUtc, post.CreatedAtUtc);
        Assert.Contains(
            await dbContext.Comments.ToListAsync(),
            comment => comment.Id == CommentId &&
                comment.PostId == PostId &&
                comment.AuthorUserId == UserTwoId &&
                comment.CreatedAtUtc == CreatedAtUtc);
        Assert.Contains(
            await dbContext.PostReactions.ToListAsync(),
            reaction => reaction.PostId == PostId &&
                reaction.UserId == UserTwoId &&
                reaction.Type == ReactionType.Love &&
                reaction.CreatedAtUtc == CreatedAtUtc);
        Assert.Contains(
            await dbContext.PostMedia.ToListAsync(),
            relation => relation.PostId == PostId &&
                relation.MediaId == PostMediaId &&
                relation.SortOrder == 0);
        Assert.Contains(
            await dbContext.MediaReferences.ToListAsync(),
            relation => relation.PostId == PostId &&
                relation.MediaId == PostMediaId &&
                relation.AttachedAtUtc == CreatedAtUtc);
        Assert.Equal(
            MediaStatus.Ready,
            (await dbContext.MediaAssets.SingleAsync(asset => asset.Id == AvatarMediaId)).Status);

        var conversation = await dbContext.Conversations.SingleAsync(candidate => candidate.Id == ConversationId);
        Assert.Equal(UserOneId, conversation.UserId1);
        Assert.Equal(UserTwoId, conversation.UserId2);
        Assert.Equal(ReadAtUtc, conversation.LastMessageAtUtc);
        Assert.Contains(
            await dbContext.Messages.ToListAsync(),
            message => message.Id == FirstMessageId &&
                message.ConversationId == ConversationId &&
                message.SenderUserId == UserOneId &&
                message.CreatedAtUtc == CreatedAtUtc);
        Assert.Contains(
            await dbContext.Messages.ToListAsync(),
            message => message.Id == LastMessageId &&
                message.ConversationId == ConversationId &&
                message.SenderUserId == UserTwoId &&
                message.ReadAtUtc == ReadAtUtc);
        var cursor = await dbContext.ConversationReadCursors.SingleAsync(candidate =>
            candidate.ConversationId == ConversationId && candidate.UserId == UserOneId);
        Assert.Equal(LastMessageId, cursor.LastReadMessageId);
        Assert.Equal(ReadAtUtc, cursor.LastReadMessageCreatedAtUtc);
        Assert.Equal(ReadAtUtc, cursor.LastReadAtUtc);
    }
}
