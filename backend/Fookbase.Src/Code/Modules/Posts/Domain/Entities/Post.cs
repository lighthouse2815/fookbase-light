using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Posts.Common;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Entities;

[Index(nameof(AuthorUserId), nameof(CreatedAtUtc))]
[Index(nameof(DeletedAtUtc), nameof(CreatedAtUtc))]
[Index(nameof(AuthorUserId), nameof(CreatedAtUtc), nameof(Id))]
[Index(nameof(AuthorUserId), nameof(IsPinned), nameof(CreatedAtUtc), nameof(Id))]
[Index(nameof(ContainerType), nameof(ContainerId), nameof(CreatedAtUtc), nameof(Id))]
[Index(nameof(PostType), nameof(CreatedAtUtc), nameof(Id))]
[Index(nameof(PostType), nameof(AuthorUserId), nameof(CreatedAtUtc), nameof(Id))]
public sealed class Post
{
    public const string AvatarUpdatedPostContent = "đã cập nhật ảnh đại diện.";
    public const string CoverUpdatedPostContent = "đã cập nhật ảnh bìa.";

    private Post() { }

    public Post(
        Guid id,
        Guid authorUserId,
        string content,
        PostPrivacy privacy,
        DateTimeOffset createdAtUtc,
        PostType postType = PostType.STANDARD)
        : this(id, authorUserId, content, privacy, PostContainerType.PROFILE, authorUserId,
            createdAtUtc, postType)
    {
    }

    public Post(
        Guid id,
        Guid authorUserId,
        string content,
        PostPrivacy privacy,
        PostContainerType containerType,
        Guid containerId,
        DateTimeOffset createdAtUtc,
        PostType postType = PostType.STANDARD)
    {
        Id = id;
        AuthorUserId = authorUserId;
        Content = PostNormalization.NormalizeContent(content);
        Privacy = privacy;
        ContainerType = containerType;
        ContainerId = containerId;
        PostType = postType;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid AuthorUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User AuthorUser { get; private set; } = null!;

    [Required]
    [MaxLength(10_000)]
    public string Content { get; private set; } = string.Empty;

    public PostPrivacy Privacy { get; private set; }

    public PostContainerType ContainerType { get; private set; }

    public Guid ContainerId { get; private set; }

    public PostType PostType { get; private set; }

    [MaxLength(32)]
    public string? TextBackground { get; private set; }

    [DefaultValue(false)]
    public bool IsPinned { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public ICollection<Comment> Comments { get; private set; } = new List<Comment>();

    public ICollection<PostHashtag> Hashtags { get; private set; } = new List<PostHashtag>();

    public ICollection<PostMedia> MediaItems { get; private set; } = new List<PostMedia>();

    public ICollection<PostReaction> Reactions { get; private set; } = new List<PostReaction>();

    public ICollection<PostSave> Saves { get; private set; } = new List<PostSave>();

    public ICollection<PostShare> Shares { get; private set; } = new List<PostShare>();

    public void Update(string content, PostPrivacy privacy, DateTimeOffset updatedAtUtc)
    {
        EnsureActive();
        Content = PostNormalization.NormalizeContent(content);
        Privacy = privacy;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void SetTextBackground(string? textBackground)
    {
        EnsureActive();
        TextBackground = string.IsNullOrWhiteSpace(textBackground) ? null : textBackground.Trim();
    }

    public void Delete(DateTimeOffset deletedAtUtc)
    {
        EnsureActive();
        DeletedAtUtc = deletedAtUtc;
    }

    public static bool IsProfileMediaUpdateContent(string content) =>
        content is AvatarUpdatedPostContent or CoverUpdatedPostContent;

    public void SetPinned(bool isPinned)
    {
        EnsureActive();
        IsPinned = isPinned;
    }

    private void EnsureActive()
    {
        if (DeletedAtUtc is not null)
        {
            throw new InvalidOperationException("A deleted post cannot be changed.");
        }
    }
}
