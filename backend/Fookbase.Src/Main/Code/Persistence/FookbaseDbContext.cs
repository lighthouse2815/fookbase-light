using Fookbase.Api.Modules.Friends.Entities;
using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Messages.Entities;
using Fookbase.Api.Modules.Notifications.Entities;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Persistence;

public sealed class FookbaseDbContext(DbContextOptions<FookbaseDbContext> options)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();

    public DbSet<Friendship> Friendships => Set<Friendship>();

    public DbSet<BlockedUser> BlockedUsers => Set<BlockedUser>();

    public DbSet<FriendNotification> FriendNotifications => Set<FriendNotification>();

    public DbSet<Group> Groups => Set<Group>();

    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();

    public DbSet<GroupJoinRequest> GroupJoinRequests => Set<GroupJoinRequest>();

    public DbSet<GroupInvite> GroupInvites => Set<GroupInvite>();

    public DbSet<GroupRule> GroupRules => Set<GroupRule>();

    public DbSet<GroupCoverMediaReference> GroupCoverMediaReferences => Set<GroupCoverMediaReference>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<ConversationReadCursor> ConversationReadCursors => Set<ConversationReadCursor>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<MessageNotification> MessageNotifications => Set<MessageNotification>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<Post> Posts => Set<Post>();

    public DbSet<Comment> Comments => Set<Comment>();

    public DbSet<CommentReaction> CommentReactions => Set<CommentReaction>();

    public DbSet<PostReaction> PostReactions => Set<PostReaction>();

    public DbSet<PostMedia> PostMedia => Set<PostMedia>();

    public DbSet<ContentReport> ContentReports => Set<ContentReport>();

    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    public DbSet<MediaReference> MediaReferences => Set<MediaReference>();

    public DbSet<ProfileMediaReference> ProfileMediaReferences => Set<ProfileMediaReference>();

    public DbSet<ObjectDeletion> ObjectDeletions => Set<ObjectDeletion>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(
            typeof(FookbaseDbContext).Assembly,
            type => type.Namespace?.StartsWith(
                "Fookbase.Api.Modules.",
                StringComparison.Ordinal) == true &&
                    type.Namespace.EndsWith(".Data.Configurations", StringComparison.Ordinal));
    }
}
