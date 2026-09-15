using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Api.Modules.Posts.Common;
using Fookbase.Api.Modules.Posts.DTOs.Requests;
using Fookbase.Api.Modules.Posts.DTOs.Responses;
using Fookbase.Api.Modules.Posts.Services;

namespace Fookbase.Api.Modules.Posts.Endpoints;

public static class PostEndpoints
{
    public static IEndpointRouteBuilder MapPostEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/posts");
        var hashtags = endpoints.MapGroup("/api/hashtags");

        group.MapPost("", CreatePostAsync).RequireAuthorization();
        group.MapGet("/saved", GetSavedPostsAsync).RequireAuthorization();
        group.MapPut("/{postId:guid}", UpdatePostAsync).RequireAuthorization();
        group.MapDelete("/{postId:guid}", DeletePostAsync).RequireAuthorization();
        group.MapPut("/{postId:guid}/pin", PinPostAsync).RequireAuthorization();
        group.MapDelete("/{postId:guid}/pin", UnpinPostAsync).RequireAuthorization();
        group.MapGet("/{postId:guid}", GetPostAsync);
        group.MapGet("/feed", GetFeedAsync).RequireAuthorization();
        group.MapGet("/search", SearchPostsAsync).RequireAuthorization();
        group.MapGet("/users/{authorUserId:guid}", GetUserPostsAsync);
        group.MapPost("/{postId:guid}/comments", CreateCommentAsync).RequireAuthorization();
        group.MapGet("/{postId:guid}/comments", GetCommentsAsync);
        group.MapGet("/{postId:guid}/reactions", GetReactionsAsync).RequireAuthorization();
        group.MapPut("/comments/{commentId:guid}", UpdateCommentAsync).RequireAuthorization();
        group.MapDelete("/comments/{commentId:guid}", DeleteCommentAsync).RequireAuthorization();
        group.MapPut("/comments/{commentId:guid}/reaction", SetCommentReactionAsync).RequireAuthorization();
        group.MapDelete("/comments/{commentId:guid}/reaction", RemoveCommentReactionAsync).RequireAuthorization();
        group.MapPut("/{postId:guid}/reaction", SetReactionAsync).RequireAuthorization();
        group.MapDelete("/{postId:guid}/reaction", RemoveReactionAsync).RequireAuthorization();
        group.MapPost("/{postId:guid}/save", SavePostAsync).RequireAuthorization();
        group.MapDelete("/{postId:guid}/save", RemoveSavedPostAsync).RequireAuthorization();
        group.MapPost("/{postId:guid}/shares", SharePostAsync).RequireAuthorization();
        group.MapGet("/{postId:guid}/media/{mediaId:guid}/access", GetMediaAccessAsync)
            .RequireAuthorization();
        hashtags.MapGet("/{tag}/posts", GetHashtagPostsAsync);

        return endpoints;
    }

    private static async Task<IResult> CreatePostAsync(
        CreatePostRequest request,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await useCase.CreatePostAsync(
            actorUserId, request.Content, request.Privacy, request.MediaIds ?? [], cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/posts/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> UpdatePostAsync(
        Guid postId,
        UpdatePostRequest request,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await useCase.UpdatePostAsync(
            actorUserId, postId, request.Content, request.Privacy, request.MediaIds ?? [], cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static Task<IResult> DeletePostAsync(
        Guid postId,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            principal,
            actorUserId => useCase.DeletePostAsync(actorUserId, postId, cancellationToken));

    private static Task<IResult> PinPostAsync(
        Guid postId,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken) =>
        ExecutePostMutationAsync(
            principal,
            actorUserId => useCase.SetPostPinnedAsync(actorUserId, postId, true, cancellationToken));

    private static Task<IResult> UnpinPostAsync(
        Guid postId,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken) =>
        ExecutePostMutationAsync(
            principal,
            actorUserId => useCase.SetPostPinnedAsync(actorUserId, postId, false, cancellationToken));

    private static async Task<IResult> GetSavedPostsAsync(
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = SocialInteractionsService.DefaultPageSize)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await useCase.GetSavedPostsAsync(actorUserId, cursor, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> SavePostAsync(
        Guid postId,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await useCase.SavePostAsync(actorUserId, postId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> RemoveSavedPostAsync(
        Guid postId,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await useCase.RemoveSavedPostAsync(actorUserId, postId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> SharePostAsync(
        Guid postId,
        CreatePostShareRequest request,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await useCase.SharePostAsync(
            actorUserId,
            postId,
            request.DestinationType,
            request.DestinationId,
            request.Caption,
            cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/posts/{postId}/shares/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetHashtagPostsAsync(
        string tag,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken,
        string? cursor = null,
        int limit = SocialInteractionsService.DefaultPageSize)
    {
        if (!TryGetViewerUserId(principal, out var viewerUserId))
        {
            return InvalidAccessToken();
        }

        var result = await useCase.GetHashtagPostsAsync(viewerUserId, tag, cursor, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetPostAsync(
        Guid postId,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (!TryGetViewerUserId(principal, out var viewerUserId))
        {
            return InvalidAccessToken();
        }

        var result = await useCase.GetPostAsync(viewerUserId, postId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetFeedAsync(
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = 20)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await useCase.GetFeedAsync(actorUserId, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> SearchPostsAsync(
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken,
        string? query = null,
        int offset = 0,
        int limit = 20)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await useCase.SearchPostsAsync(actorUserId, query, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetUserPostsAsync(
        Guid authorUserId,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = 20)
    {
        if (!TryGetViewerUserId(principal, out var viewerUserId))
        {
            return InvalidAccessToken();
        }

        var result = await useCase.GetUserPostsAsync(
            viewerUserId, authorUserId, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> CreateCommentAsync(
        Guid postId,
        CreateCommentRequest request,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await useCase.CreateCommentAsync(
            actorUserId, postId, request.ParentCommentId, request.Content, cancellationToken);
        return result.Succeeded
            ? Results.Created($"/api/posts/comments/{result.Value!.Id}", result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> UpdateCommentAsync(
        Guid commentId,
        UpdateCommentRequest request,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await useCase.UpdateCommentAsync(
            actorUserId, commentId, request.Content, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static Task<IResult> DeleteCommentAsync(
        Guid commentId,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            principal,
            actorUserId => useCase.DeleteCommentAsync(actorUserId, commentId, cancellationToken));

    private static async Task<IResult> GetCommentsAsync(
        Guid postId,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken,
        int offset = 0,
        int limit = 20)
    {
        if (!TryGetViewerUserId(principal, out var viewerUserId))
        {
            return InvalidAccessToken();
        }

        var result = await useCase.GetCommentsAsync(
            viewerUserId, postId, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetReactionsAsync(
        Guid postId,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken,
        string? type = null,
        int offset = 0,
        int limit = 100)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await useCase.GetReactionsAsync(
            actorUserId, postId, type, offset, limit, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> SetReactionAsync(
        Guid postId,
        SetReactionRequest request,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await useCase.SetReactionAsync(
            actorUserId, postId, request.Type, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> SetCommentReactionAsync(
        Guid commentId,
        SetReactionRequest request,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return InvalidAccessToken();

        var result = await useCase.SetCommentReactionAsync(
            actorUserId, commentId, request.Type, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> RemoveReactionAsync(
        Guid postId,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await useCase.RemoveReactionAsync(actorUserId, postId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> RemoveCommentReactionAsync(
        Guid commentId,
        ClaimsPrincipal principal,
        PostsUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return InvalidAccessToken();

        var result = await useCase.RemoveCommentReactionAsync(actorUserId, commentId, cancellationToken);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> GetMediaAccessAsync(
        Guid postId, Guid mediaId, ClaimsPrincipal principal, PostsUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var actorUserId)) return InvalidAccessToken();
        var result = await useCase.GetMediaAccessAsync(actorUserId, postId, mediaId, cancellationToken);
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

    private static async Task<IResult> ExecutePostMutationAsync(
        ClaimsPrincipal principal,
        Func<Guid, Task<ApplicationResult<PostResponse>>> command)
    {
        if (!TryGetActorUserId(principal, out var actorUserId))
        {
            return InvalidAccessToken();
        }

        var result = await command(actorUserId);
        return result.Succeeded ? Results.Ok(result.Value) : result.Error!.ToHttpResult();
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
