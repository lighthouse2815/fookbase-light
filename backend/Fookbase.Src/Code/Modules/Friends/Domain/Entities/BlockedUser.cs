using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Friends.Entities;

[PrimaryKey(nameof(BlockerUserId), nameof(BlockedUserId))]
[Index(nameof(BlockedUserId))]
public sealed class BlockedUser
{
    private BlockedUser()
    {
    }

    private BlockedUser(Guid blockerUserId, Guid blockedUserId, DateTimeOffset createdAtUtc)
    {
        BlockerUserId = blockerUserId;
        BlockedUserId = blockedUserId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid BlockerUserId { get; private set; }

    public Guid BlockedUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    [ForeignKey(nameof(BlockerUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User BlockerUser { get; private set; } = null!;

    [ForeignKey(nameof(BlockedUserId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User BlockedAccount { get; private set; } = null!;

    public static BlockedUser Create(
        Guid blockerUserId,
        Guid blockedUserId,
        DateTimeOffset createdAtUtc)
    {
        if (blockerUserId == blockedUserId)
        {
            throw new ArgumentException("A user cannot block themselves.");
        }

        return new BlockedUser(blockerUserId, blockedUserId, createdAtUtc);
    }
}
