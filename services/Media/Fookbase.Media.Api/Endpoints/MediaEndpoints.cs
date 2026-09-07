using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Fookbase.Media.Api.Authentication;
using Fookbase.Media.Application.Media;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Media.Api.Endpoints;

public static class MediaEndpoints
{
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/media");
        group.MapPost("/uploads", CreateUploadAsync).RequireAuthorization();
        group.MapPost("/{mediaId:guid}/complete", CompleteAsync).RequireAuthorization();
        group.MapGet("/{mediaId:guid}", GetMetadataAsync).RequireAuthorization();
        group.MapDelete("/{mediaId:guid}", DeleteAsync).RequireAuthorization();
        endpoints.MapPost("/internal/media/{mediaId:guid}/read-url", CreateReadUrlAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateUploadAsync(
        CreateUploadRequest request, ClaimsPrincipal principal, IMediaService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var ownerUserId)) return Results.Unauthorized();
        var result = await service.CreateUploadAsync(ownerUserId, request, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/media/{result.Value!.MediaId}", result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> CompleteAsync(
        Guid mediaId, ClaimsPrincipal principal, IMediaService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var ownerUserId)) return Results.Unauthorized();
        var result = await service.CompleteAsync(ownerUserId, mediaId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetMetadataAsync(
        Guid mediaId, ClaimsPrincipal principal, IMediaService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var ownerUserId)) return Results.Unauthorized();
        var result = await service.GetMetadataAsync(ownerUserId, mediaId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> CreateReadUrlAsync(
        Guid mediaId, HttpRequest request, IMediaService service,
        InternalServicesOptions options, CancellationToken cancellationToken)
    {
        if (!request.Headers.TryGetValue("X-Internal-Service-Token", out var supplied) ||
            !FixedTimeEquals(supplied.ToString(), options.Token))
        {
            return Results.Unauthorized();
        }
        var result = await service.CreateReadUrlAsync(mediaId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> DeleteAsync(
        Guid mediaId, ClaimsPrincipal principal, IMediaService service, CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var ownerUserId)) return Results.Unauthorized();
        var result = await service.DeleteAsync(ownerUserId, mediaId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static bool TryGetActorUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);

    private static bool FixedTimeEquals(string supplied, string expected)
    {
        var left = Encoding.UTF8.GetBytes(supplied);
        var right = Encoding.UTF8.GetBytes(expected);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
