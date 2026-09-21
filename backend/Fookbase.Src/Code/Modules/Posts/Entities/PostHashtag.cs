namespace Fookbase.Api.Modules.Posts.Entities;

public sealed class PostHashtag
{
    private PostHashtag()
    {
    }

    private PostHashtag(Guid postId, Guid hashtagId)
    {
        PostId = postId;
        HashtagId = hashtagId;
    }

    public Guid PostId { get; private set; }

    public Guid HashtagId { get; private set; }

    public static PostHashtag Create(Guid postId, Guid hashtagId) => new(postId, hashtagId);
}
