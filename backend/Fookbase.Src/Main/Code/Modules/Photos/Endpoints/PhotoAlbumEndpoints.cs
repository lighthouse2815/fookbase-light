using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Media.Common;
using Fookbase.Api.Modules.Photos.DTOs.Requests;
using Fookbase.Api.Modules.Photos.Services;

namespace Fookbase.Api.Modules.Photos.Endpoints;

public static class PhotoAlbumEndpoints
{
    public static IEndpointRouteBuilder MapPhotoAlbumEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var albums = endpoints.MapGroup("/api/albums");
        albums.MapPost("", Create).RequireAuthorization();
        albums.MapGet("/{albumId:guid}", Get);
        albums.MapPatch("/{albumId:guid}", Update).RequireAuthorization();
        albums.MapDelete("/{albumId:guid}", Delete).RequireAuthorization();
        albums.MapPost("/{albumId:guid}/media", AddMedia).RequireAuthorization();
        albums.MapGet("/{albumId:guid}/media", GetMedia);
        albums.MapGet("/{albumId:guid}/media/{mediaId:guid}", GetPhoto);
        albums.MapPatch("/{albumId:guid}/media/{mediaId:guid}", UpdateCaption).RequireAuthorization();
        albums.MapDelete("/{albumId:guid}/media/{mediaId:guid}", RemoveMedia).RequireAuthorization();
        endpoints.MapGet("/api/users/{userId:guid}/albums", GetUserAlbums);
        return endpoints;
    }

    private static async Task<IResult> Create(CreatePhotoAlbumRequest request, ClaimsPrincipal principal, PhotosService service, CancellationToken ct)
    { var actor = Actor(principal); if (actor is null) return Results.Unauthorized(); var result = await service.CreateAsync(actor.Value, request, ct); return result.Succeeded ? Results.Created($"/api/albums/{result.Value!.Id}", result.Value) : result.Error!.ToHttpResult(); }
    private static async Task<IResult> Get(Guid albumId, ClaimsPrincipal principal, PhotosService service, CancellationToken ct)
    { var result = await service.GetAsync(albumId, Actor(principal), ct); return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult(); }
    private static async Task<IResult> Update(Guid albumId, UpdatePhotoAlbumRequest request, ClaimsPrincipal principal, PhotosService service, CancellationToken ct)
    { var actor = Actor(principal); if (actor is null) return Results.Unauthorized(); var result = await service.UpdateAsync(actor.Value, albumId, request, ct); return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult(); }
    private static async Task<IResult> Delete(Guid albumId, ClaimsPrincipal principal, PhotosService service, CancellationToken ct)
    { var actor = Actor(principal); if (actor is null) return Results.Unauthorized(); var result = await service.DeleteAsync(actor.Value, albumId, ct); return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult(); }
    private static async Task<IResult> AddMedia(Guid albumId, AddAlbumMediaRequest request, ClaimsPrincipal principal, PhotosService service, CancellationToken ct)
    { var actor = Actor(principal); if (actor is null) return Results.Unauthorized(); var result = await service.AddMediaAsync(actor.Value, albumId, request.MediaId, ct); return result.Succeeded ? Results.Created($"/api/albums/{albumId}/media/{request.MediaId}", result.Value) : result.Error!.ToHttpResult(); }
    private static async Task<IResult> UpdateCaption(Guid albumId, Guid mediaId, UpdateAlbumMediaRequest request, ClaimsPrincipal principal, PhotosService service, CancellationToken ct)
    { var actor = Actor(principal); if (actor is null) return Results.Unauthorized(); var result = await service.UpdateCaptionAsync(actor.Value, albumId, mediaId, request, ct); return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult(); }
    private static async Task<IResult> RemoveMedia(Guid albumId, Guid mediaId, ClaimsPrincipal principal, PhotosService service, CancellationToken ct)
    { var actor = Actor(principal); if (actor is null) return Results.Unauthorized(); var result = await service.RemoveMediaAsync(actor.Value, albumId, mediaId, ct); return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult(); }
    private static async Task<IResult> GetUserAlbums(Guid userId, ClaimsPrincipal principal, PhotosService service, CancellationToken ct, string? cursor = null, int limit = PhotosService.DefaultPageSize)
    { var result = await service.GetUserAlbumsAsync(userId, Actor(principal), cursor, limit, ct); return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult(); }
    private static async Task<IResult> GetMedia(Guid albumId, ClaimsPrincipal principal, PhotosService service, CancellationToken ct, string? cursor = null, int limit = PhotosService.DefaultPageSize)
    { var result = await service.GetMediaAsync(albumId, Actor(principal), cursor, limit, ct); return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult(); }
    private static async Task<IResult> GetPhoto(Guid albumId, Guid mediaId, ClaimsPrincipal principal, PhotosService service, CancellationToken ct)
    { var result = await service.GetPhotoAsync(albumId, mediaId, Actor(principal), ct); return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult(); }
    private static Guid? Actor(ClaimsPrincipal principal) => Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;
}
