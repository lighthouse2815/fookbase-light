using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Persistence.Annotations;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Friends.Entities;

[PrimaryKey(nameof(BlockerUserId), nameof(BlockedAccountId))]
[Index(nameof(BlockedAccountId))]
[CheckConstraint("CK_BlockedUsers_DifferentUsers", "\"BlockerUserId\" <> \"BlockedUserId\"")]
public sealed class BlockedUser
{
    private BlockedUser() { }

    public BlockedUser(
        Guid blockerUserId,
        Guid blockedUserId,
        DateTimeOffset createdAtUtc)
    {
        if (blockerUserId == blockedUserId)
        {
            throw new ArgumentException("A user cannot block themselves.");
        }

        BlockerUserId = blockerUserId;
        BlockedAccountId = blockedUserId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid BlockerUserId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User BlockerUser { get; private set; } = null!;

    [Column("BlockedUserId")]
    public Guid BlockedAccountId { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User BlockedAccount { get; private set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; private set; }
}
