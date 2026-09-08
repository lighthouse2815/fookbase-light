using Fookbase.Api.Modules.Posts.Services.Common;

namespace Fookbase.Api.Modules.Posts.Services.Posts;

public interface IPostsService
{
    Task<ApplicationResult<PostResponse>> CreatePostAsync(Guid actorUserId, string content, string privacy, IReadOnlyList<Guid> mediaIds, CancellationToken cancellationToken = default);
    Task<ApplicationResult<PostResponse>> UpdatePostAsync(Guid actorUserId, Guid postId, string content, string privacy, IReadOnlyList<Guid> mediaIds, CancellationToken cancellationToken = default);
    Task<ApplicationResult> DeletePostAsync(Guid actorUserId, Guid postId, CancellationToken cancellationToken = default);
    Task<ApplicationResult<PostResponse>> GetPostAsync(Guid? viewerUserId, Guid postId, CancellationToken cancellationToken = default);
    Task<ApplicationResult<PagedResponse<PostResponse>>> GetFeedAsync(Guid actorUserId, int offset, int limit, CancellationToken cancellationToken = default);
    Task<ApplicationResult<PagedResponse<PostResponse>>> GetUserPostsAsync(Guid? viewerUserId, Guid authorUserId, int offset, int limit, CancellationToken cancellationToken = default);
    Task<ApplicationResult<CommentResponse>> CreateCommentAsync(Guid actorUserId, Guid postId, Guid? parentCommentId, string content, CancellationToken cancellationToken = default);
    Task<ApplicationResult<CommentResponse>> UpdateCommentAsync(Guid actorUserId, Guid commentId, string content, CancellationToken cancellationToken = default);
    Task<ApplicationResult> DeleteCommentAsync(Guid actorUserId, Guid commentId, CancellationToken cancellationToken = default);
    Task<ApplicationResult<PagedResponse<CommentResponse>>> GetCommentsAsync(Guid? viewerUserId, Guid postId, int offset, int limit, CancellationToken cancellationToken = default);
    Task<ApplicationResult<PostResponse>> SetReactionAsync(Guid actorUserId, Guid postId, string reactionType, CancellationToken cancellationToken = default);
    Task<ApplicationResult<PostResponse>> RemoveReactionAsync(Guid actorUserId, Guid postId, CancellationToken cancellationToken = default);
    Task<ApplicationResult<MediaAccessResponse>> GetMediaAccessAsync(Guid actorUserId, Guid postId, Guid mediaId, CancellationToken cancellationToken = default);
}
