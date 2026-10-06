using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Fookbase.Api.Modules.Friends.Domain.ValueObjects;

namespace Fookbase.Api.Modules.Friends.Entities;

[Index(nameof(UserId1), nameof(UserId2), IsUnique = true)]
[Index(nameof(UserId1))]
[Index(nameof(UserId2))]
public sealed class Friendship
{
    private Friendship()
    {
    }

    private Friendship(Guid id, UserPair pair, DateTimeOffset createdAtUtc)
    {
        Id = id;
        UserId1 = pair.UserId1;
        UserId2 = pair.UserId2;
        CreatedAtUtc = createdAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }

    public Guid UserId1 { get; private set; }

    public Guid UserId2 { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    [ForeignKey(nameof(UserId1))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User1 { get; private set; } = null!;

    [ForeignKey(nameof(UserId2))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User2 { get; private set; } = null!;

    public static Friendship Create(
        Guid id,
        Guid firstUserId,
        Guid secondUserId,
        DateTimeOffset createdAtUtc) =>
        new(id, UserPair.Create(firstUserId, secondUserId), createdAtUtc);

    public bool Contains(Guid userId) => UserId1 == userId || UserId2 == userId;

    public Guid OtherUserId(Guid userId) =>
        UserId1 == userId
            ? UserId2
            : UserId2 == userId
                ? UserId1
                : throw new UnauthorizedAccessException("The user is not part of this friendship.");
}
