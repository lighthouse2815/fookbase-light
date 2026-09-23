using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Users.DTOs.Requests;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Users.Services;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Friends.Services;

namespace Fookbase.Api.Modules.Users.Endpoints;

public static class UserProfileEndpoints
{
    public static IEndpointRouteBuilder MapUserProfileEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/users");
        var followGroup = endpoints.MapGroup("/api/users").RequireAuthorization();
        var birthdayGroup = endpoints.MapGroup("/api/birthdays").RequireAuthorization();

        followGroup.MapPost("/{userId:guid}/follow", FollowAsync);
        followGroup.MapDelete("/{userId:guid}/follow", UnfollowAsync);
        followGroup.MapGet("/{userId:guid}/followers", GetFollowersAsync);
        followGroup.MapGet("/{userId:guid}/following", GetFollowingAsync);
        followGroup.MapGet("/{userId:guid}/friends", GetFriendsAsync);
        group.MapGet("/search", SearchAsync).AllowAnonymous();
        group.MapGet("/{userId:guid}/avatar", GetAvatarAsync).AllowAnonymous();
        group.MapGet("/{userId:guid}/cover", GetCoverAsync).AllowAnonymous();
        group.MapGet("/{userId:guid}", GetByIdAsync).AllowAnonymous();
        group.MapGet("/me", GetCurrentAsync).RequireAuthorization();
        group.MapPatch("/me", UpdateCurrentAsync).RequireAuthorization();
        birthdayGroup.MapGet("/today", GetTodaysBirthdaysAsync);
        birthdayGroup.MapGet("/upcoming", GetUpcomingBirthdaysAsync);

        return endpoints;
    }

    private static async Task<IResult> SearchAsync(
        string? query,
        ClaimsPrincipal principal,
        UserProfileService profileService,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = 20)
    {
        var viewerUserId = TryGetUserId(principal, out var userId) ? (Guid?)userId : null;
        var result = await profileService.SearchAsync(query, offset, limit, viewerUserId, cancellationToken);
        return result.Succeeded
            ? Results.Ok(result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> FollowAsync(
        Guid userId,
        ClaimsPrincipal principal,
        FriendsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.FollowAsync(actorUserId, userId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> UnfollowAsync(
        Guid userId,
        ClaimsPrincipal principal,
        FriendsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.UnfollowAsync(actorUserId, userId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetFollowersAsync(
        Guid userId,
        ClaimsPrincipal principal,
        FriendsService service,
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = FriendsService.DefaultFollowPageSize)
    {
        if (!TryGetUserId(principal, out var viewerUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.GetFollowersAsync(viewerUserId, userId, cursor, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetFollowingAsync(
        Guid userId,
        ClaimsPrincipal principal,
        FriendsService service,
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = FriendsService.DefaultFollowPageSize)
    {
        if (!TryGetUserId(principal, out var viewerUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.GetFollowingAsync(viewerUserId, userId, cursor, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetFriendsAsync(
        Guid userId,
        ClaimsPrincipal principal,
        FriendsService service,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = 20)
    {
        if (!TryGetUserId(principal, out var viewerUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.GetVisibleFriendsAsync(viewerUserId, userId, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetByIdAsync(
        Guid userId,
        ClaimsPrincipal principal,
        UserProfileService profileService,
        CancellationToken cancellationToken)
    {
        var viewerUserId = TryGetUserId(principal, out var currentUserId) ? (Guid?)currentUserId : null;
        var result = await profileService.GetAsync(userId, viewerUserId, cancellationToken);
        return result.Succeeded
            ? Results.Ok(result.Value)
            : result.Error!.ToHttpResult();
    }

    private static Task<IResult> GetAvatarAsync(
        Guid userId,
        UserProfileService profileService,
        MediaService mediaService,
        CancellationToken cancellationToken) =>
        GetProfileMediaAsync(
            () => profileService.GetAvatarMediaIdAsync(userId, cancellationToken),
            mediaService,
            cancellationToken);

    private static Task<IResult> GetCoverAsync(
        Guid userId,
        UserProfileService profileService,
        MediaService mediaService,
        CancellationToken cancellationToken) =>
        GetProfileMediaAsync(
            () => profileService.GetCoverMediaIdAsync(userId, cancellationToken),
            mediaService,
            cancellationToken);

    private static async Task<IResult> GetProfileMediaAsync(
        Func<Task<Guid?>> getMediaId,
        MediaService mediaService,
        CancellationToken cancellationToken)
    {
        var mediaId = await getMediaId();
        if (mediaId is null)
        {
            return Results.NotFound();
        }

        var readUrl = await mediaService.CreateReadUrlAsync(mediaId.Value, cancellationToken);
        return readUrl.Succeeded ? Results.Redirect(readUrl.Value!.Url) : Results.NotFound();
    }

    private static async Task<IResult> GetCurrentAsync(
        ClaimsPrincipal principal,
        UserProfileService profileService,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await profileService.GetAsync(userId, userId, cancellationToken);
        return result.Succeeded
            ? Results.Ok(result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> UpdateCurrentAsync(
        UpdateUserProfileRequest request,
        ClaimsPrincipal principal,
        UserProfileService profileService,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await profileService.UpdateAsync(userId, request, cancellationToken);
        return result.Succeeded
            ? Results.Ok(result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetTodaysBirthdaysAsync(
        ClaimsPrincipal principal,
        UserProfileService profileService,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
        return Results.Ok(await profileService.GetTodaysBirthdaysAsync(userId, cancellationToken));
    }

    private static async Task<IResult> GetUpcomingBirthdaysAsync(
        int? days,
        ClaimsPrincipal principal,
        UserProfileService profileService,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
        var result = await profileService.GetUpcomingBirthdaysAsync(userId, days ?? 7, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
