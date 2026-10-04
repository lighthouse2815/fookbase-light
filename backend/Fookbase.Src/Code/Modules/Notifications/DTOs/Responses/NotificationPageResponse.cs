namespace Fookbase.Api.Modules.Notifications.DTOs.Responses;

public sealed record NotificationResponse(
    Guid Id,
    Guid RecipientUserId,
    Guid? ActorUserId,
    string? ActorUsername,
    string? ActorDisplayName,
    string Type,
    string? EntityType,
    Guid? EntityId,
    Guid? ParentEntityId,
    bool IsRead,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc,
    string? ActorAvatarUrl = null,
    string? PageUsername = null);

public sealed record NotificationPageResponse(
    IReadOnlyList<NotificationResponse> Items,
    string? NextCursor);
