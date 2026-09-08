using Fookbase.Api.Modules.Friends.Services.Common;

namespace Fookbase.Api.Modules.Friends.Services.Relationships;

public interface IFriendsService
{
    Task<ApplicationResult<FriendRequestResponse>> SendRequestAsync(Guid actorUserId, Guid receiverUserId, CancellationToken cancellationToken = default);
    Task<ApplicationResult<FriendResponse>> AcceptRequestAsync(Guid actorUserId, Guid requestId, CancellationToken cancellationToken = default);
    Task<ApplicationResult> DeclineRequestAsync(Guid actorUserId, Guid requestId, CancellationToken cancellationToken = default);
    Task<ApplicationResult> CancelRequestAsync(Guid actorUserId, Guid requestId, CancellationToken cancellationToken = default);
    Task<ApplicationResult> UnfriendAsync(Guid actorUserId, Guid otherUserId, CancellationToken cancellationToken = default);
    Task<ApplicationResult> BlockAsync(Guid actorUserId, Guid blockedUserId, CancellationToken cancellationToken = default);
    Task<ApplicationResult> UnblockAsync(Guid actorUserId, Guid blockedUserId, CancellationToken cancellationToken = default);
    Task<ApplicationResult<PagedResponse<FriendResponse>>> GetFriendsAsync(Guid actorUserId, int offset, int limit, CancellationToken cancellationToken = default);
    Task<ApplicationResult<PagedResponse<FriendRequestResponse>>> GetIncomingRequestsAsync(Guid actorUserId, int offset, int limit, CancellationToken cancellationToken = default);
    Task<ApplicationResult<PagedResponse<FriendRequestResponse>>> GetOutgoingRequestsAsync(Guid actorUserId, int offset, int limit, CancellationToken cancellationToken = default);
    Task<ApplicationResult<PagedResponse<BlockedUserResponse>>> GetBlockedUsersAsync(Guid actorUserId, int offset, int limit, CancellationToken cancellationToken = default);
    Task<ApplicationResult<RelationshipStatusResponse>> GetStatusAsync(Guid actorUserId, Guid otherUserId, CancellationToken cancellationToken = default);
    Task<ApplicationResult<MutualFriendsResponse>> GetMutualFriendsAsync(Guid actorUserId, Guid otherUserId, int offset, int limit, CancellationToken cancellationToken = default);
}
