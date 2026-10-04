using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Stories.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Stories.Entities;

[Table("StoryReactions")]
[PrimaryKey(nameof(StoryId), nameof(UserId))]
[Index(nameof(StoryId), nameof(CreatedAtUtc))]
public sealed class StoryReaction
{
    private StoryReaction() { }

    private StoryReaction(Guid storyId, Guid userId, StoryReactionType type, DateTimeOffset createdAtUtc)
    {
        StoryId = storyId;
        UserId = userId;
        Type = type;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid StoryId { get; private set; }
    public Guid UserId { get; private set; }
    public StoryReactionType Type { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [ForeignKey(nameof(StoryId))]
    [InverseProperty(nameof(Story.Reactions))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Story Story { get; private set; } = null!;

    [ForeignKey(nameof(UserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User { get; private set; } = null!;

    public static StoryReaction Create(Guid storyId, Guid userId, StoryReactionType type, DateTimeOffset createdAtUtc) =>
        new(storyId, userId, type, createdAtUtc);

    public void Change(StoryReactionType type, DateTimeOffset createdAtUtc)
    {
        Type = type;
        CreatedAtUtc = createdAtUtc;
    }
}
