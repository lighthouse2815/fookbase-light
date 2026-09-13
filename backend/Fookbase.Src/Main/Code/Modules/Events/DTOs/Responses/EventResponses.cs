using Fookbase.Api.Modules.Posts.DTOs.Responses;
namespace Fookbase.Api.Modules.Events.DTOs.Responses;
public sealed record EventHostResponse(string Type, Guid Id, string Name, string? Username = null, string? AvatarUrl = null);
public sealed record EventResponse(Guid Id, string Name, string? Description, EventHostResponse DisplayHost, string Privacy,
    string LocationType, string? LocationName, string? Address, string? OnlineUrl, DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc, string Status, string? CoverUrl, int GoingCount, int InterestedCount,
    string? ViewerRsvpStatus, bool CanManage, bool CanPost, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);
public sealed record EventCursorPageResponse<T>(IReadOnlyList<T> Items, string? NextCursor);
public sealed record EventParticipantResponse(Guid UserId, string Username, string DisplayName, string? AvatarUrl,
    string Status, DateTimeOffset RespondedAtUtc);
public sealed record EventInvitationResponse(Guid Id, Guid EventId, Guid InviterUserId, Guid InviteeUserId, string Status,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? RespondedAtUtc, EventResponse? Event = null);
