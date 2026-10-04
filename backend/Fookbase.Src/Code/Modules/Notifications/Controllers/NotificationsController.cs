using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Notifications.DTOs.Requests;
using Fookbase.Api.Modules.Notifications.DTOs.Responses;
using Fookbase.Api.Modules.Notifications.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Notifications.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(
    NotificationService notificationService,
    PushNotificationService pushNotificationService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetNotificationsAsync(
        CancellationToken cancellationToken,
        [FromQuery] string? before = null,
        [FromQuery] int limit = NotificationService.DefaultPageSize)
    {
        if (!TryGetUserId(User, out var userId))
        {
            return Unauthorized();
        }

        if (limit is < 1 or > NotificationService.MaximumPageSize ||
            !NotificationService.IsValidCursor(before))
        {
            return BadRequest(new
            {
                code = "invalid_notification_cursor",
                message = "The notification cursor or limit is invalid."
            });
        }

        return Ok(await notificationService.GetNotificationsAsync(userId, before, limit, cancellationToken));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCountAsync(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(User, out var userId))
        {
            return Unauthorized();
        }

        return Ok(new NotificationCountResponse(
            await notificationService.GetUnreadCountAsync(userId, cancellationToken)));
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(User, out var userId))
        {
            return Unauthorized();
        }

        return await notificationService.MarkReadAsync(userId, notificationId, cancellationToken)
            ? NoContent()
            : NotFound(new { code = "notification_not_found" });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllReadAsync(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(User, out var userId))
        {
            return Unauthorized();
        }

        await notificationService.MarkAllReadAsync(userId, cancellationToken);
        return NoContent();
    }

    [HttpPost("push-tokens/zola")]
    public async Task<IActionResult> RegisterZolaPushTokenAsync(
        [FromBody] PushTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(User, out var userId))
        {
            return Unauthorized();
        }

        return await pushNotificationService.RegisterZolaDeviceAsync(userId, request.Token, cancellationToken)
            ? NoContent()
            : BadRequest(new { code = "invalid_push_token", message = "The Expo push token is invalid." });
    }

    [HttpDelete("push-tokens/zola")]
    public async Task<IActionResult> UnregisterZolaPushTokenAsync(
        [FromBody] PushTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(User, out var userId))
        {
            return Unauthorized();
        }

        await pushNotificationService.UnregisterZolaDeviceAsync(userId, request.Token, cancellationToken);
        return NoContent();
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
