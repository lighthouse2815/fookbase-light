namespace Fookbase.Api.Modules.Posts.Entities;

public sealed class PostSave
{
    private PostSave()
    {
    }

    private PostSave(Guid userId, Guid postId, DateTimeOffset savedAtUtc)
    {
        UserId = userId;
        PostId = postId;
        SavedAtUtc = savedAtUtc;
    }

    public Guid UserId { get; private set; }

    public Guid PostId { get; private set; }

    public DateTimeOffset SavedAtUtc { get; private set; }

    public static PostSave Create(Guid userId, Guid postId, DateTimeOffset savedAtUtc) =>
        new(userId, postId, savedAtUtc);
}
