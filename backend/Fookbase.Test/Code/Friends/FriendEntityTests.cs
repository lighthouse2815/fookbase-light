using Fookbase.Api.Modules.Friends.Domain.Enums;
using Fookbase.Api.Modules.Friends.Entities;

namespace Fookbase.Friends.Api.IntegrationTests;

public sealed class FriendEntityTests
{
    [Fact]
    public void Constructors_reject_relationships_with_the_same_user()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => new FriendRequest(userId, userId, now));
        Assert.Throws<ArgumentException>(() => new Friendship(userId, userId, now));
        Assert.Throws<ArgumentException>(() => new BlockedUser(userId, userId, now));
        Assert.Throws<ArgumentException>(() => new UserFollow(userId, userId, now));
    }

    [Fact]
    public void Constructors_canonicalize_pairs_without_reversing_request_direction()
    {
        var lowerUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var higherUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var now = DateTimeOffset.UtcNow;
        var request = new FriendRequest(higherUserId, lowerUserId, now);
        var friendship = new Friendship(higherUserId, lowerUserId, now);

        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.NotEqual(Guid.Empty, friendship.Id);
        Assert.Equal(higherUserId, request.SenderUserId);
        Assert.Equal(lowerUserId, request.ReceiverUserId);
        Assert.Equal(lowerUserId, request.User1Id);
        Assert.Equal(higherUserId, request.User2Id);
        Assert.Equal(FriendRequestStatus.PENDING, request.Status);
        Assert.Null(request.RespondedAtUtc);
        Assert.Equal(request.User1Id, friendship.User1Id);
        Assert.Equal(request.User2Id, friendship.User2Id);
        Assert.Equal(now, request.CreatedAtUtc);
        Assert.Equal(now, friendship.CreatedAtUtc);
    }

    [Fact]
    public void New_request_preserves_receiver_authorization_and_pending_validation()
    {
        var senderUserId = Guid.NewGuid();
        var receiverUserId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var request = new FriendRequest(senderUserId, receiverUserId, now);

        Assert.Throws<UnauthorizedAccessException>(() => request.Accept(senderUserId, now));
        Assert.Equal(FriendRequestStatus.PENDING, request.Status);
        request.Accept(receiverUserId, now);
        Assert.Equal(FriendRequestStatus.ACCEPTED, request.Status);
        Assert.Equal(now, request.RespondedAtUtc);
        Assert.Throws<InvalidOperationException>(() => request.Decline(receiverUserId, now));
    }
}
