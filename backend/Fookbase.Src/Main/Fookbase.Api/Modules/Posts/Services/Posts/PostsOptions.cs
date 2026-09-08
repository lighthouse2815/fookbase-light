namespace Fookbase.Api.Modules.Posts.Services.Posts;

public sealed class PostsOptions
{
    public const string SectionName = "Posts";
    public int MaximumAttachments { get; init; } = 10;
    public void Validate()
    {
        if (MaximumAttachments <= 0) throw new InvalidOperationException("Maximum attachments must be positive.");
    }
}
