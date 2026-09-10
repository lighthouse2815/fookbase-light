using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Notifications.Services;

namespace Fookbase.Api.Modules.Notifications.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/notifications").RequireAuthorization();
        group.MapGet("", GetNotificationsAsync);
        group.MapGet("/unread-count", GetUnreadCountAsync);
        group.MapPost("/{notificationId:guid}/read", MarkReadAsync);
        group.MapPost("/read-all", MarkAllReadAsync);
        return endpoints;
    }

    private static async Task<IResult> GetNotificationsAsync(
        ClaimsPrincipal principal,
        NotificationService service,
        CancellationToken cancellationToken,
        string? before = null,
        int limit = NotificationService.DefaultPageSize)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        if (limit is < 1 or > NotificationService.MaximumPageSize ||
            !NotificationService.IsValidCursor(before))
        {
            return Results.BadRequest(new
            {
                code = "invalid_notification_cursor",
                message = "The notification cursor or limit is invalid."
            });
        }

        return Results.Ok(await service.GetNotificationsAsync(userId, before, limit, cancellationToken));
    }

    private static async Task<IResult> GetUnreadCountAsync(
        ClaimsPrincipal principal,
        NotificationService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new
        {
            unreadNotificationCount = await service.GetUnreadCountAsync(userId, cancellationToken)
        });
    }

    private static async Task<IResult> MarkReadAsync(
        Guid notificationId,
        ClaimsPrincipal principal,
        NotificationService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        return await service.MarkReadAsync(userId, notificationId, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound(new { code = "notification_not_found" });
    }

    private static async Task<IResult> MarkAllReadAsync(
        ClaimsPrincipal principal,
        NotificationService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        await service.MarkAllReadAsync(userId, cancellationToken);
        return Results.NoContent();
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
