using Fookbase.Api.Modules.Users.Common;
using Fookbase.Api.Modules.Users.DTOs.Requests;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Users.Services;
using Fookbase.Api.Modules.Media.Services;

namespace Fookbase.Api.Modules.Users.Endpoints;

public static class UserProfileEndpoints
{
    public static IEndpointRouteBuilder MapUserProfileEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/users");

        group.MapGet("/search", SearchAsync).AllowAnonymous();
        group.MapGet("/{userId:guid}/avatar", GetAvatarAsync).AllowAnonymous();
        group.MapGet("/{userId:guid}/cover", GetCoverAsync).AllowAnonymous();
        group.MapGet("/{userId:guid}", GetByIdAsync).AllowAnonymous();
        group.MapGet("/me", GetCurrentAsync).RequireAuthorization();
        group.MapPatch("/me", UpdateCurrentAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> SearchAsync(
        string? query,
        UserProfileService profileService,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = 20)
    {
        var result = await profileService.SearchAsync(query, offset, limit, cancellationToken);
        return result.Succeeded
            ? Results.Ok(result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetByIdAsync(
        Guid userId,
        UserProfileService profileService,
        CancellationToken cancellationToken)
    {
        var result = await profileService.GetAsync(userId, cancellationToken);
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

        var result = await profileService.GetAsync(userId, cancellationToken);
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

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
