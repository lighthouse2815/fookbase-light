using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Messages.Common;
using Fookbase.Api.Modules.Messages.DTOs.Requests;
using Fookbase.Api.Modules.Messages.Services;

namespace Fookbase.Api.Modules.Messages.Endpoints;

public static class MessageEndpoints
{
    public static IEndpointRouteBuilder MapMessageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/messages").RequireAuthorization();

        group.MapPost("/conversations/{userId:guid}", GetOrCreateConversationAsync);
        group.MapGet("/conversations", GetConversationsAsync);
        group.MapGet("/notifications", GetUnreadNotificationsAsync);
        group.MapGet("/conversations/{conversationId:guid}/messages", GetMessagesAsync);
        group.MapPost("/conversations/{conversationId:guid}/read", MarkConversationReadAsync);
        group.MapPost("/conversations/{conversationId:guid}/messages", SendMessageAsync);

        return endpoints;
    }

    private static async Task<IResult> GetOrCreateConversationAsync(
        Guid userId,
        ClaimsPrincipal principal,
        MessagesService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.GetOrCreateConversationAsync(actorUserId, userId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetConversationsAsync(
        ClaimsPrincipal principal,
        MessagesService service,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = 20)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.GetConversationsAsync(actorUserId, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetMessagesAsync(
        Guid conversationId,
        ClaimsPrincipal principal,
        MessagesService service,
        CancellationToken cancellationToken,
        string? before = null,
        int limit = 50)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.GetMessagesAsync(
            actorUserId,
            conversationId,
            before,
            limit,
            cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> MarkConversationReadAsync(
        Guid conversationId,
        MarkConversationReadRequest request,
        ClaimsPrincipal principal,
        MessagesService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.MarkConversationReadAsync(
            actorUserId,
            conversationId,
            request,
            cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetUnreadNotificationsAsync(
        ClaimsPrincipal principal,
        MessagesService service,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = 100)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.GetUnreadNotificationsAsync(
            actorUserId,
            offset,
            limit,
            cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> SendMessageAsync(
        Guid conversationId,
        SendMessageRequest request,
        ClaimsPrincipal principal,
        MessagesService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.SendMessageAsync(
            actorUserId,
            conversationId,
            request.Content,
            cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/messages/conversations/{conversationId}/messages/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    private static bool TryGetActorUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);

    private static IResult InvalidAccessToken() => Results.Unauthorized();
}
