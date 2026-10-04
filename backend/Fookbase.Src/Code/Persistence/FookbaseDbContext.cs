using Fookbase.Api.Modules.Friends.Entities;
using Fookbase.Api.Modules.Admin.Entities;
using Fookbase.Api.Modules.Events.Entities;
using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Messages.Entities;
using Fookbase.Api.Modules.Notifications.Entities;
using Fookbase.Api.Modules.Pages.Entities;
using Fookbase.Api.Modules.Photos.Entities;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Reels.Entities;
using Fookbase.Api.Modules.Stories.Entities;
using Fookbase.Api.Modules.Users.Entities;
using Fookbase.Api.Persistence.Conventions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace Fookbase.Api.Persistence;

public sealed class FookbaseDbContext(DbContextOptions<FookbaseDbContext> options)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<TwoFactorLoginChallenge> TwoFactorLoginChallenges => Set<TwoFactorLoginChallenge>();
    public DbSet<ExternalLoginTicket> ExternalLoginTickets => Set<ExternalLoginTicket>();
    public DbSet<RegistrationChallenge> RegistrationChallenges => Set<RegistrationChallenge>();
    public DbSet<PasswordResetOtp> PasswordResetOtps => Set<PasswordResetOtp>();
    public DbSet<ModerationAction> ModerationActions => Set<ModerationAction>();
    public DbSet<UserModerationState> UserModerationStates => Set<UserModerationState>();

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<UserPrivacySettings> UserPrivacySettings => Set<UserPrivacySettings>();

    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();

    public DbSet<Friendship> Friendships => Set<Friendship>();

    public DbSet<UserFollow> UserFollows => Set<UserFollow>();

    public DbSet<BlockedUser> BlockedUsers => Set<BlockedUser>();

    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventParticipant> EventParticipants => Set<EventParticipant>();
    public DbSet<EventInvitation> EventInvitations => Set<EventInvitation>();
    public DbSet<EventCoverMediaReference> EventCoverMediaReferences => Set<EventCoverMediaReference>();

    public DbSet<FriendNotification> FriendNotifications => Set<FriendNotification>();

    public DbSet<Group> Groups => Set<Group>();

    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();

    public DbSet<GroupJoinRequest> GroupJoinRequests => Set<GroupJoinRequest>();

    public DbSet<GroupInvite> GroupInvites => Set<GroupInvite>();

    public DbSet<GroupRule> GroupRules => Set<GroupRule>();

    public DbSet<GroupCoverMediaReference> GroupCoverMediaReferences => Set<GroupCoverMediaReference>();

    public DbSet<Page> Pages => Set<Page>();

    public DbSet<PageMember> PageMembers => Set<PageMember>();

    public DbSet<PageRoleInvitation> PageRoleInvitations => Set<PageRoleInvitation>();

    public DbSet<PageFollower> PageFollowers => Set<PageFollower>();

    public DbSet<PageMediaReference> PageMediaReferences => Set<PageMediaReference>();

    public DbSet<PhotoAlbum> PhotoAlbums => Set<PhotoAlbum>();

    public DbSet<AlbumMedia> AlbumMedia => Set<AlbumMedia>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<ConversationReadCursor> ConversationReadCursors => Set<ConversationReadCursor>();

    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();

    public DbSet<MessageReaction> MessageReactions => Set<MessageReaction>();

    public DbSet<MessageNotification> MessageNotifications => Set<MessageNotification>();

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PushDevice> PushDevices => Set<PushDevice>();
    public DbSet<PushDeliveryReceipt> PushDeliveryReceipts => Set<PushDeliveryReceipt>();

    public DbSet<Post> Posts => Set<Post>();

    public DbSet<Comment> Comments => Set<Comment>();

    public DbSet<CommentReaction> CommentReactions => Set<CommentReaction>();

    public DbSet<PostReaction> PostReactions => Set<PostReaction>();

    public DbSet<PostMedia> PostMedia => Set<PostMedia>();

    public DbSet<PostSave> PostSaves => Set<PostSave>();

    public DbSet<PostShare> PostShares => Set<PostShare>();

    public DbSet<ContentMention> ContentMentions => Set<ContentMention>();

    public DbSet<Hashtag> Hashtags => Set<Hashtag>();

    public DbSet<PostHashtag> PostHashtags => Set<PostHashtag>();

    public DbSet<ContentReport> ContentReports => Set<ContentReport>();

    public DbSet<ReelView> ReelViews => Set<ReelView>();

    public DbSet<Story> Stories => Set<Story>();

    public DbSet<StoryMediaReference> StoryMediaReferences => Set<StoryMediaReference>();

    public DbSet<StoryView> StoryViews => Set<StoryView>();

    public DbSet<StoryReaction> StoryReactions => Set<StoryReaction>();

    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    public DbSet<MediaReference> MediaReferences => Set<MediaReference>();

    public DbSet<ProfileMediaReference> ProfileMediaReferences => Set<ProfileMediaReference>();

    public DbSet<ObjectDeletion> ObjectDeletions => Set<ObjectDeletion>();

    public DbSet<MediaProcessingJob> MediaProcessingJobs => Set<MediaProcessingJob>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Conventions.Add(services => new DefaultValueAttributeConvention(
            services.GetRequiredService<ProviderConventionSetBuilderDependencies>()));
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<User>(entity =>
        {
            entity.HasIndex(user => user.NormalizedEmail)
                .HasDatabaseName("EmailIndex")
                .IsUnique();

            entity.HasIndex(user => user.PhoneNumber)
                .HasDatabaseName("PhoneNumberIndex")
                .IsUnique();
        });

        builder.Entity<Post>(entity =>
        {
            entity.HasIndex(post => new { post.AuthorUserId, post.CreatedAtUtc, post.Id })
                .HasFilter("\"DeletedAtUtc\" IS NULL");
            entity.HasIndex(post => new { post.AuthorUserId, post.IsPinned, post.CreatedAtUtc, post.Id })
                .HasFilter("\"DeletedAtUtc\" IS NULL");
            entity.HasIndex(post => new { post.ContainerType, post.ContainerId, post.CreatedAtUtc, post.Id })
                .HasFilter("\"DeletedAtUtc\" IS NULL");
            entity.HasIndex(post => new { post.PostType, post.CreatedAtUtc, post.Id })
                .HasFilter("\"DeletedAtUtc\" IS NULL");
            entity.HasIndex(post => new { post.PostType, post.AuthorUserId, post.CreatedAtUtc, post.Id })
                .HasFilter("\"DeletedAtUtc\" IS NULL");
        });

        builder.ApplyConfigurationsFromAssembly(
            typeof(FookbaseDbContext).Assembly,
            type => type.Namespace?.StartsWith(
                "Fookbase.Api.Modules.",
                StringComparison.Ordinal) == true &&
                    type.Namespace.EndsWith(".Data.Configurations", StringComparison.Ordinal));
    }
}
