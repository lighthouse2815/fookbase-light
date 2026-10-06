using Fookbase.Api.Modules.Posts.Entities;

namespace Fookbase.Api.Modules.Posts.Common;

public static class PostNormalization
{
    public static string NormalizeContent(string content)
    {
        var normalized = content.Trim();
        if (normalized.Length > Post.MaximumContentLength)
        {
            throw new ArgumentException(
                $"Post content cannot exceed {Post.MaximumContentLength} characters.");
        }

        return normalized;
    }
}
