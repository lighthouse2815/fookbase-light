using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Posts.Services.Common;
using Fookbase.Api.Modules.Posts.Services.Posts;

namespace Fookbase.Api.Modules.Posts.Endpoints;

public static class PostEndpoints
{
    public static IEndpointRouteBuilder MapPostEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/posts");

        group.MapPost("", CreatePostAsync).RequireAuthorization();
        group.MapPut("/{postId:guid}", UpdatePostAsync).RequireAuthorization();
        group.MapDelete("/{postId:guid}", DeletePostAsync).RequireAuthorization();
        group.MapGet("/{postId:guid}", GetPostAsync);
        group.MapGet("/feed", GetFeedAsync).RequireAuthorization();
        group.MapGet("/users/{authorUserId:guid}", GetUserPostsAsync);
        group.MapPost("/{postId:guid}/comments", CreateCommentAsync).RequireAuthorization();
        group.MapGet("/{postId:guid}/comments", GetCommentsAsync);
        group.MapPut("/comments/{commentId:guid}", UpdateCommentAsync).RequireAuthorization();
        group.MapDelete("/comments/{commentId:guid}", DeleteCommentAsync).RequireAuthorization();
        group.MapPut("/{postId:guid}/reaction", SetReactionAsync).RequireAuthorization();
        group.MapDelete("/{postId:guid}/reaction", RemoveReactionAsync).RequireAuthorization();
        group.MapGet("/{postId:guid}/media/{mediaId:guid}/access", GetMediaAccessAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> CreatePostAsync(
        CreatePostRequest request,
        ClaimsPrincipal principal,
        IPostsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.CreatePostAsync(
            actorUserId, request.Content, request.Privacy, request.MediaIds ?? [], cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/posts/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> UpdatePostAsync(
        Guid postId,
        UpdatePostRequest request,
        ClaimsPrincipal principal,
        IPostsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.UpdatePostAsync(
            actorUserId, postId, request.Content, request.Privacy, request.MediaIds ?? [], cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static Task<IResult> DeletePostAsync(
        Guid postId,
        ClaimsPrincipal principal,
        IPostsService service,
        CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            principal,
            actorUserId => service.DeletePostAsync(actorUserId, postId, cancellationToken));

    private static async Task<IResult> GetPostAsync(
        Guid postId,
        ClaimsPrincipal principal,
        IPostsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetViewerUserId(principal, out var viewerUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.GetPostAsync(viewerUserId, postId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetFeedAsync(
        ClaimsPrincipal principal,
        IPostsService service,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = 20)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.GetFeedAsync(actorUserId, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetUserPostsAsync(
        Guid authorUserId,
        ClaimsPrincipal principal,
        IPostsService service,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = 20)
    {
        if (!TryGetViewerUserId(principal, out var viewerUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.GetUserPostsAsync(
            viewerUserId, authorUserId, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> CreateCommentAsync(
        Guid postId,
        CreateCommentRequest request,
        ClaimsPrincipal principal,
        IPostsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.CreateCommentAsync(
            actorUserId, postId, request.ParentCommentId, request.Content, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/posts/comments/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> UpdateCommentAsync(
        Guid commentId,
        UpdateCommentRequest request,
        ClaimsPrincipal principal,
        IPostsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.UpdateCommentAsync(
            actorUserId, commentId, request.Content, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static Task<IResult> DeleteCommentAsync(
        Guid commentId,
        ClaimsPrincipal principal,
        IPostsService service,
        CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            principal,
            actorUserId => service.DeleteCommentAsync(actorUserId, commentId, cancellationToken));

    private static async Task<IResult> GetCommentsAsync(
        Guid postId,
        ClaimsPrincipal principal,
        IPostsService service,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = 20)
    {
        if (!TryGetViewerUserId(principal, out var viewerUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.GetCommentsAsync(
            viewerUserId, postId, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> SetReactionAsync(
        Guid postId,
        SetReactionRequest request,
        ClaimsPrincipal principal,
        IPostsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.SetReactionAsync(
            actorUserId, postId, request.Type, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> RemoveReactionAsync(
        Guid postId,
        ClaimsPrincipal principal,
        IPostsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await service.RemoveReactionAsync(actorUserId, postId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetMediaAccessAsync(
        Guid postId, Guid mediaId, ClaimsPrincipal principal, IPostsService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return InvalidAccessToken();
        var result = await service.GetMediaAccessAsync(actorUserId, postId, mediaId, cancellationToken);
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

    private static bool TryGetViewerUserId(ClaimsPrincipal principal, out Guid? userId)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            userId = null;
            return true;
        }

        var parsed = TryGetActorUserId(principal, out var actorUserId);
        userId = parsed ? actorUserId : null;
        return parsed;
    }

    private static bool TryGetActorUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);

    private static IResult InvalidAccessToken() => Results.Unauthorized();
}

public sealed record CreatePostRequest(string Content, string Privacy, IReadOnlyList<Guid>? MediaIds = null);

public sealed record UpdatePostRequest(string Content, string Privacy, IReadOnlyList<Guid>? MediaIds = null);

public sealed record CreateCommentRequest(string Content, Guid? ParentCommentId);

public sealed record UpdateCommentRequest(string Content);

public sealed record SetReactionRequest(string Type);
