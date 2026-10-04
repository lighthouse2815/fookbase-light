using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Messages.DTOs.Requests;
using Fookbase.Api.Modules.Messages.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Messages.Controllers;

[ApiController]
[Authorize]
[Route("api/messages")]
public sealed class MessagesController(MessagesService service) : ControllerBase
{
    [HttpPost("conversations/{userId:guid}")]
    public Task<IActionResult> StartDirectLegacyAsync(Guid userId, CancellationToken cancellationToken) =>
        StartDirectCoreAsync(userId, cancellationToken);

    [HttpPost("conversations/direct")]
    public Task<IActionResult> StartDirectAsync([FromBody] CreateDirectConversationRequest request, CancellationToken cancellationToken) =>
        StartDirectCoreAsync(request.UserId, cancellationToken);

    private async Task<IActionResult> StartDirectCoreAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.GetOrCreateDirectConversationAsync(actorUserId, userId, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPost("conversations/group")]
    public async Task<IActionResult> CreateGroupAsync([FromBody] CreateGroupConversationRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.CreateGroupConversationAsync(actorUserId, request, cancellationToken);
        return result.Succeeded ? Created($"/api/messages/conversations/{result.Value!.Id}", result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversationsAsync(CancellationToken cancellationToken, string? before = null, int limit = 20, bool includeArchived = false)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.GetConversationsAsync(actorUserId, before, limit, includeArchived, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<IActionResult> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.GetConversationAsync(actorUserId, conversationId, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPatch("conversations/{conversationId:guid}")]
    public async Task<IActionResult> UpdateConversationAsync(Guid conversationId, [FromBody] UpdateConversationRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.UpdateConversationAsync(actorUserId, conversationId, request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpGet("conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken, string? before = null, int limit = 50)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.GetMessagesAsync(actorUserId, conversationId, before, limit, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> SendMessageAsync(Guid conversationId, [FromBody] SendMessageRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.SendMessageAsync(actorUserId, conversationId, request, cancellationToken);
        return result.Succeeded ? Created($"/api/messages/conversations/{conversationId}/messages/{result.Value!.Id}", result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpGet("conversations/{conversationId:guid}/search")]
    public async Task<IActionResult> SearchMessagesAsync(Guid conversationId, CancellationToken cancellationToken, string? q = null)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.SearchMessagesAsync(actorUserId, conversationId, q, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPost("conversations/{conversationId:guid}/read")]
    public async Task<IActionResult> MarkConversationReadAsync(Guid conversationId, [FromBody] MarkConversationReadRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.MarkConversationReadAsync(actorUserId, conversationId, request, cancellationToken);
        return result.Succeeded ? NoContent() : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPost("conversations/{conversationId:guid}/participants")]
    public async Task<IActionResult> AddParticipantsAsync(Guid conversationId, [FromBody] AddConversationParticipantsRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.AddParticipantsAsync(actorUserId, conversationId, request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpDelete("conversations/{conversationId:guid}/participants/{userId:guid}")]
    public async Task<IActionResult> RemoveParticipantAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.RemoveParticipantAsync(actorUserId, conversationId, userId, cancellationToken);
        return result.Succeeded ? NoContent() : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPatch("conversations/{conversationId:guid}/participants/{userId:guid}/role")]
    public async Task<IActionResult> ChangeParticipantRoleAsync(Guid conversationId, Guid userId, [FromBody] ChangeConversationParticipantRoleRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.ChangeParticipantRoleAsync(actorUserId, conversationId, userId, request, cancellationToken);
        return result.Succeeded ? NoContent() : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPost("conversations/{conversationId:guid}/leave")]
    public async Task<IActionResult> LeaveConversationAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.LeaveConversationAsync(actorUserId, conversationId, cancellationToken);
        return result.Succeeded ? NoContent() : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPost("conversations/{conversationId:guid}/transfer-ownership")]
    public async Task<IActionResult> TransferOwnershipAsync(Guid conversationId, [FromBody] TransferConversationOwnershipRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.TransferOwnershipAsync(actorUserId, conversationId, request, cancellationToken);
        return result.Succeeded ? NoContent() : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPost("{messageId:guid}/reactions")]
    public async Task<IActionResult> SetReactionAsync(Guid messageId, [FromBody] SetMessageReactionRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.SetReactionAsync(actorUserId, messageId, request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpDelete("{messageId:guid}/reactions")]
    public async Task<IActionResult> RemoveReactionAsync(Guid messageId, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.RemoveReactionAsync(actorUserId, messageId, cancellationToken);
        return result.Succeeded ? NoContent() : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpPatch("{messageId:guid}")]
    public async Task<IActionResult> EditMessageAsync(Guid messageId, [FromBody] EditMessageRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.EditMessageAsync(actorUserId, messageId, request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpDelete("{messageId:guid}")]
    public async Task<IActionResult> DeleteMessageAsync(Guid messageId, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.DeleteMessageAsync(actorUserId, messageId, cancellationToken);
        return result.Succeeded ? NoContent() : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpGet("media/{mediaId:guid}/read-url")]
    public async Task<IActionResult> CreateMediaReadUrlAsync(Guid mediaId, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.CreateMessageMediaReadUrlAsync(actorUserId, mediaId, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetUnreadNotificationsAsync(CancellationToken cancellationToken, int offset = 0, int limit = 100)
    {
        if (!TryGetActorUserId(User, out var actorUserId)) return Unauthorized();
        var result = await service.GetUnreadNotificationsAsync(actorUserId, offset, limit, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : new ObjectResult(result.Error!.ToProblemDetails()) { StatusCode = result.Error!.ToStatusCode() };
    }

    private static bool TryGetActorUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
