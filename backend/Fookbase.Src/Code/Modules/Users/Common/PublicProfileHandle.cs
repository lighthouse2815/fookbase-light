namespace Fookbase.Api.Modules.Users.Common;

public static class PublicProfileHandle
{
    public static string From(string username) => IsPrivateContact(username) ? string.Empty : username;

    public static bool IsPrivateContact(string value)
    {
        if (value.Contains('@', StringComparison.Ordinal))
        {
            return true;
        }

        var digitCount = 0;
        foreach (var character in value)
        {
            if (char.IsDigit(character))
            {
                digitCount++;
                continue;
            }

            if (!char.IsWhiteSpace(character) && character is not ('+' or '-' or '(' or ')' or '.'))
            {
                return false;
            }
        }

        return digitCount >= 8;
    }
}
