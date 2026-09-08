namespace Fookbase.Api.Modules.Identity.Services;

public sealed record RegisterRequest(string? Email, string? Username, string? Password);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record RefreshRequest(string? RefreshToken);

public sealed record LogoutRequest(string? RefreshToken);

public sealed record AuthenticatedUserResponse(Guid Id, string Email, string Username);

public sealed record AuthenticationResponse(
    AuthenticatedUserResponse User,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
