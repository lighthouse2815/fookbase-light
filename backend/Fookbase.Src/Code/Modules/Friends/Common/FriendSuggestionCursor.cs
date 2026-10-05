using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Fookbase.Api.Modules.Friends.Common;

internal sealed record FriendSuggestionCursor(int Version, Guid ViewerUserId, int Score, Guid UserId)
{
    public const int CurrentVersion = 1;

    public static FriendSuggestionCursor? DecodeOrNull(
        string? value, Guid viewerUserId, IDataProtectionProvider protectionProvider)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            if (value.Length > 4_096) throw new FormatException();
            var cursor = JsonSerializer.Deserialize<FriendSuggestionCursor>(
                CreateProtector(protectionProvider, viewerUserId).Unprotect(value)) ?? throw new FormatException();
            return cursor.Version == CurrentVersion && cursor.ViewerUserId == viewerUserId
                ? cursor
                : throw new FormatException();
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or ArgumentException)
        {
            throw new FormatException("The friend suggestion cursor is invalid.", exception);
        }
    }

    public string Encode(Guid viewerUserId, IDataProtectionProvider protectionProvider) =>
        CreateProtector(protectionProvider, viewerUserId).Protect(JsonSerializer.Serialize(this));

    private static IDataProtector CreateProtector(IDataProtectionProvider protectionProvider, Guid viewerUserId) =>
        protectionProvider.CreateProtector("Fookbase.FriendSuggestions", CurrentVersion.ToString(), viewerUserId.ToString("N"));
}
