namespace Fookbase.Api.Modules.Posts.Services;

public enum PostsServiceError
{
    NONE,
    POST_NOT_FOUND,
    COMMENT_NOT_FOUND,
    PARENT_COMMENT_NOT_FOUND,
    FORBIDDEN,
    RELATIONSHIP_BLOCKED,
    INVALID_PARENT_COMMENT,
    MEDIA_NOT_ATTACHED,
    INVALID_POST_TYPE,
    PROFILE_MEDIA_POST_NOT_EDITABLE
}

public sealed record PostsServiceResult<T>(T? Value, PostsServiceError Error)
{
    public bool Succeeded => Error == PostsServiceError.NONE;

    public static PostsServiceResult<T> Success(T value) => new(value, PostsServiceError.NONE);

    public static PostsServiceResult<T> Failure(PostsServiceError error) => new(default, error);
}
