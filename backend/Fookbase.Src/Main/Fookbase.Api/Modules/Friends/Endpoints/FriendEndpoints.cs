using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Friends.Services.Common;
using Fookbase.Api.Modules.Friends.Services.Relationships;

namespace Fookbase.Api.Modules.Friends.Endpoints;

public static class FriendEndpoints
{
    public static IEndpointRouteBuilder MapFriendEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/friends").RequireAuthorization();

        group.MapPost("/requests/{userId:guid}", SendRequestAsync);
        group.MapDelete("/requests/{requestId:guid}", CancelRequestAsync);
        group.MapPost("/requests/{requestId:guid}/accept", AcceptRequestAsync);
        group.MapPost("/requests/{requestId:guid}/decline", DeclineRequestAsync);
        group.MapGet("/requests/incoming", GetIncomingRequestsAsync);
        group.MapGet("/requests/outgoing", GetOutgoingRequestsAsync);
        group.MapDelete("/{userId:guid}", UnfriendAsync);
        group.MapGet("", GetFriendsAsync);
        group.MapGet("/status/{userId:guid}", GetStatusAsync);
        group.MapGet("/mutual/{userId:guid}", GetMutualFriendsAsync);
        group.MapPost("/blocks/{userId:guid}", BlockAsync);
        group.MapDelete("/blocks/{userId:guid}", UnblockAsync);
        group.MapGet("/blocks", GetBlockedUsersAsync);

        return endpoints;
    }

    private static async Task<IResult> SendRequestAsync(
        Guid userId,
        ClaimsPrincipal principal,
        IFriendsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.SendRequestAsync(actorUserId, userId, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/friends/requests/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> AcceptRequestAsync(
        Guid requestId,
        ClaimsPrincipal principal,
        IFriendsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.AcceptRequestAsync(actorUserId, requestId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static Task<IResult> DeclineRequestAsync(
        Guid requestId,
        ClaimsPrincipal principal,
        IFriendsService service,
        CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            principal,
            actorUserId => service.DeclineRequestAsync(actorUserId, requestId, cancellationToken));

    private static Task<IResult> CancelRequestAsync(
        Guid requestId,
        ClaimsPrincipal principal,
        IFriendsService service,
        CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            principal,
            actorUserId => service.CancelRequestAsync(actorUserId, requestId, cancellationToken));

    private static Task<IResult> UnfriendAsync(
        Guid userId,
        ClaimsPrincipal principal,
        IFriendsService service,
        CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            principal,
            actorUserId => service.UnfriendAsync(actorUserId, userId, cancellationToken));

    private static Task<IResult> BlockAsync(
        Guid userId,
        ClaimsPrincipal principal,
        IFriendsService service,
        CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            principal,
            actorUserId => service.BlockAsync(actorUserId, userId, cancellationToken));

    private static Task<IResult> UnblockAsync(
        Guid userId,
        ClaimsPrincipal principal,
        IFriendsService service,
        CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            principal,
            actorUserId => service.UnblockAsync(actorUserId, userId, cancellationToken));

    private static async Task<IResult> GetFriendsAsync(
        ClaimsPrincipal principal,
        IFriendsService service,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = 20)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.GetFriendsAsync(actorUserId, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetIncomingRequestsAsync(
        ClaimsPrincipal principal,
        IFriendsService service,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = 20)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.GetIncomingRequestsAsync(actorUserId, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetOutgoingRequestsAsync(
        ClaimsPrincipal principal,
        IFriendsService service,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = 20)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.GetOutgoingRequestsAsync(actorUserId, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetBlockedUsersAsync(
        ClaimsPrincipal principal,
        IFriendsService service,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = 20)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.GetBlockedUsersAsync(actorUserId, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetStatusAsync(
        Guid userId,
        ClaimsPrincipal principal,
        IFriendsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.GetStatusAsync(actorUserId, userId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetMutualFriendsAsync(
        Guid userId,
        ClaimsPrincipal principal,
        IFriendsService service,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = 20)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.GetMutualFriendsAsync(
            actorUserId,
            userId,
            offset,
            limit,
            cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> ExecuteCommandAsync(
        ClaimsPrincipal principal,
        Func<Guid, Task<ApplicationResult>> command)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await command(actorUserId);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static bool TryGetActorUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);

    private static IResult InvalidAccessToken() => Results.Unauthorized();
}
