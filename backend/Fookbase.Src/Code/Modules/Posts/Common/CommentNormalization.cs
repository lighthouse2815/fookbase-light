namespace Fookbase.Api.Modules.Posts.Common;

public static class CommentNormalization
{
    public static string NormalizeContent(string content)
    {
        var normalized = content.Trim();
        if (normalized.Length is 0 or > 5_000)
        {
            throw new ArgumentException(
                "Comment content must contain between 1 and 5000 characters.");
        }

        return normalized;
    }
}
