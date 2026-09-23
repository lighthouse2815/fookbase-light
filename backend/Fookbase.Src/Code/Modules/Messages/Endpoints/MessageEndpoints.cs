using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Messages.DTOs.Requests;
using Fookbase.Api.Modules.Messages.Services;

namespace Fookbase.Api.Modules.Messages.Endpoints;

public static class MessageEndpoints
{
    public static IEndpointRouteBuilder MapMessageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/messages").RequireAuthorization();

        // Legacy direct route stays available while clients migrate to /direct.
        group.MapPost("/conversations/{userId:guid}", StartDirectLegacyAsync);
        group.MapPost("/conversations/direct", StartDirectAsync);
        group.MapPost("/conversations/group", CreateGroupAsync);
        group.MapGet("/conversations", GetConversationsAsync);
        group.MapGet("/conversations/{conversationId:guid}", GetConversationAsync);
        group.MapPatch("/conversations/{conversationId:guid}", UpdateConversationAsync);
        group.MapGet("/conversations/{conversationId:guid}/messages", GetMessagesAsync);
        group.MapPost("/conversations/{conversationId:guid}/messages", SendMessageAsync);
        group.MapGet("/conversations/{conversationId:guid}/search", SearchMessagesAsync);
        group.MapPost("/conversations/{conversationId:guid}/read", MarkConversationReadAsync);
        group.MapPost("/conversations/{conversationId:guid}/participants", AddParticipantsAsync);
        group.MapDelete("/conversations/{conversationId:guid}/participants/{userId:guid}", RemoveParticipantAsync);
        group.MapPatch("/conversations/{conversationId:guid}/participants/{userId:guid}/role", ChangeParticipantRoleAsync);
        group.MapPost("/conversations/{conversationId:guid}/leave", LeaveConversationAsync);
        group.MapPost("/conversations/{conversationId:guid}/transfer-ownership", TransferOwnershipAsync);
        group.MapPost("/{messageId:guid}/reactions", SetReactionAsync);
        group.MapDelete("/{messageId:guid}/reactions", RemoveReactionAsync);
        group.MapPatch("/{messageId:guid}", EditMessageAsync);
        group.MapDelete("/{messageId:guid}", DeleteMessageAsync);
        group.MapGet("/media/{mediaId:guid}/read-url", CreateMediaReadUrlAsync);
        group.MapGet("/notifications", GetUnreadNotificationsAsync);

        return endpoints;
    }

    private static Task<IResult> StartDirectLegacyAsync(Guid userId, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken) =>
        StartDirectCoreAsync(userId, principal, service, cancellationToken);

    private static Task<IResult> StartDirectAsync(CreateDirectConversationRequest request, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken) =>
        StartDirectCoreAsync(request.UserId, principal, service, cancellationToken);

    private static async Task<IResult> StartDirectCoreAsync(Guid userId, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.GetOrCreateDirectConversationAsync(actorUserId, userId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> CreateGroupAsync(CreateGroupConversationRequest request, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.CreateGroupConversationAsync(actorUserId, request, cancellationToken);
        return result.Succeeded ? Results.Created($"/api/messages/conversations/{result.Value!.Id}", result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetConversationsAsync(ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken, string? before = null, int limit = 20, bool includeArchived = false)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.GetConversationsAsync(actorUserId, before, limit, includeArchived, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetConversationAsync(Guid conversationId, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.GetConversationAsync(actorUserId, conversationId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> UpdateConversationAsync(Guid conversationId, UpdateConversationRequest request, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.UpdateConversationAsync(actorUserId, conversationId, request, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetMessagesAsync(Guid conversationId, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken, string? before = null, int limit = 50)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.GetMessagesAsync(actorUserId, conversationId, before, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> SendMessageAsync(Guid conversationId, SendMessageRequest request, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.SendMessageAsync(actorUserId, conversationId, request, cancellationToken);
        return result.Succeeded ? Results.Created($"/api/messages/conversations/{conversationId}/messages/{result.Value!.Id}", result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> SearchMessagesAsync(Guid conversationId, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken, string? q = null)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.SearchMessagesAsync(actorUserId, conversationId, q, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> MarkConversationReadAsync(Guid conversationId, MarkConversationReadRequest request, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.MarkConversationReadAsync(actorUserId, conversationId, request, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> AddParticipantsAsync(Guid conversationId, AddConversationParticipantsRequest request, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.AddParticipantsAsync(actorUserId, conversationId, request, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> RemoveParticipantAsync(Guid conversationId, Guid userId, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.RemoveParticipantAsync(actorUserId, conversationId, userId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> ChangeParticipantRoleAsync(Guid conversationId, Guid userId, ChangeConversationParticipantRoleRequest request, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.ChangeParticipantRoleAsync(actorUserId, conversationId, userId, request, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> LeaveConversationAsync(Guid conversationId, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.LeaveConversationAsync(actorUserId, conversationId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> TransferOwnershipAsync(Guid conversationId, TransferConversationOwnershipRequest request, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.TransferOwnershipAsync(actorUserId, conversationId, request, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> SetReactionAsync(Guid messageId, SetMessageReactionRequest request, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.SetReactionAsync(actorUserId, messageId, request, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> RemoveReactionAsync(Guid messageId, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.RemoveReactionAsync(actorUserId, messageId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> EditMessageAsync(Guid messageId, EditMessageRequest request, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.EditMessageAsync(actorUserId, messageId, request, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> DeleteMessageAsync(Guid messageId, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.DeleteMessageAsync(actorUserId, messageId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> CreateMediaReadUrlAsync(Guid mediaId, ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.CreateMessageMediaReadUrlAsync(actorUserId, mediaId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetUnreadNotificationsAsync(ClaimsPrincipal principal, MessagesService service, CancellationToken cancellationToken, int offset = 0, int limit = 100)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return Results.Unauthorized();
        var result = await service.GetUnreadNotificationsAsync(actorUserId, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static bool TryGetActorUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
