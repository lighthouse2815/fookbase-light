using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Fookbase.Api.Modules.Friends.Common;

internal sealed record FollowCursor(DateTimeOffset FollowedAtUtc, Guid UserId)
{
    public static FollowCursor? DecodeOrNull(
        string? value, Guid viewerUserId, string direction, Guid targetUserId, IDataProtectionProvider protectionProvider)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            if (value.Length > 4_096) throw new FormatException();
            return JsonSerializer.Deserialize<FollowCursor>(
                CreateProtector(protectionProvider, viewerUserId, direction, targetUserId).Unprotect(value))
                ?? throw new FormatException();
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or ArgumentException)
        {
            throw new FormatException("The follow cursor is invalid.", exception);
        }
    }

    public string Encode(
        Guid viewerUserId, string direction, Guid targetUserId, IDataProtectionProvider protectionProvider) =>
        CreateProtector(protectionProvider, viewerUserId, direction, targetUserId).Protect(JsonSerializer.Serialize(this));

    private static IDataProtector CreateProtector(
        IDataProtectionProvider protectionProvider, Guid viewerUserId, string direction, Guid targetUserId) =>
        protectionProvider.CreateProtector(
            "Fookbase.Follows", "1", viewerUserId.ToString("N"), direction, targetUserId.ToString("N"));
}
