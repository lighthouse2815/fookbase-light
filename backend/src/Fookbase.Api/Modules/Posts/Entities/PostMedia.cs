namespace Fookbase.Api.Modules.Posts.Entities;

public sealed class PostMedia
{
    private PostMedia() { }
    private PostMedia(Guid postId, Guid mediaId, int sortOrder)
    {
        PostId = postId; MediaId = mediaId; SortOrder = sortOrder;
    }
    public Guid PostId { get; private set; }
    public Guid MediaId { get; private set; }
    public int SortOrder { get; private set; }
    public static PostMedia Create(Guid postId, Guid mediaId, int sortOrder) =>
        new(postId, mediaId, sortOrder);
    public void ChangeSortOrder(int sortOrder) => SortOrder = sortOrder;
}
