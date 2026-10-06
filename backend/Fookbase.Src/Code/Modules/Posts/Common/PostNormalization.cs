namespace Fookbase.Api.Modules.Posts.Common;

public static class PostNormalization
{
    public static string NormalizeContent(string content)
    {
        var normalized = content.Trim();
        if (normalized.Length > 10_000)
        {
            throw new ArgumentException(
                "Post content cannot exceed 10000 characters.");
        }

        return normalized;
    }
}
