namespace Fookbase.Api.Modules.Notifications.DTOs.Responses;

public sealed record NotificationPageResponse(
    IReadOnlyList<NotificationResponse> Items,
    string? NextCursor);
