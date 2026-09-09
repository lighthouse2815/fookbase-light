namespace Fookbase.Api.Modules.Posts.Services;

public enum PostsServiceError
{
    None,
    UserNotFound,
    PostNotFound,
    CommentNotFound,
    ParentCommentNotFound,
    Forbidden,
    RelationshipBlocked,
    InvalidParentComment,
    InvalidMedia,
    MediaNotOwned,
    MediaNotAttached
}

public sealed record PostsServiceResult<T>(T? Value, PostsServiceError Error)
{
    public bool Succeeded => Error == PostsServiceError.None;

    public static PostsServiceResult<T> Success(T value) => new(value, PostsServiceError.None);

    public static PostsServiceResult<T> Failure(PostsServiceError error) => new(default, error);
}
