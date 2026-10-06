using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Friends.Domain.ValueObjects;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Persistence.Annotations;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Friends.Entities;

[Index(nameof(User1Id), nameof(User2Id), IsUnique = true)]
[Index(nameof(User1Id))]
[Index(nameof(User2Id))]
[CheckConstraint("CK_Friendships_CanonicalPair", "\"UserId1\" < \"UserId2\"")]
public sealed class Friendship
{
    private Friendship() { }

    public Friendship(
        Guid firstUserId,
        Guid secondUserId,
        DateTimeOffset createdAtUtc)
    {
        var pair = UserPair.Create(firstUserId, secondUserId);
        Id = Guid.NewGuid();
        User1Id = pair.UserId1;
        User2Id = pair.UserId2;
        CreatedAtUtc = createdAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }

    [Column("UserId1")]
    public Guid User1Id { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User1 { get; private set; } = null!;

    [Column("UserId2")]
    public Guid User2Id { get; private set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User User2 { get; private set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public bool Contains(Guid userId) => User1Id == userId || User2Id == userId;

    public Guid OtherUserId(Guid userId) =>
        User1Id == userId
            ? User2Id
            : User2Id == userId
                ? User1Id
                : throw new UnauthorizedAccessException("The user is not part of this friendship.");
}
