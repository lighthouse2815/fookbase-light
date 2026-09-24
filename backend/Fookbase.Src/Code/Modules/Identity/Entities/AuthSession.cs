using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Identity.Entities;

[Index(nameof(UserId), nameof(ExpiresAtUtc))]
public sealed class AuthSession
{
    private AuthSession() { }

    public AuthSession(Guid userId, string? userAgent, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        UserAgent = SanitizeUserAgent(userAgent);
        CreatedAtUtc = now;
        LastSeenAtUtc = now;
        ExpiresAtUtc = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    [MaxLength(256)]
    public string? UserAgent { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastSeenAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public bool IsActiveAt(DateTimeOffset now) => RevokedAtUtc is null && ExpiresAtUtc > now;
    public void Touch(DateTimeOffset now) => LastSeenAtUtc = now;
    public void Revoke(DateTimeOffset now) => RevokedAtUtc ??= now;

    private static string? SanitizeUserAgent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var sanitized = value.Trim().Replace("\r", string.Empty).Replace("\n", string.Empty);
        return sanitized[..Math.Min(256, sanitized.Length)];
    }
}
