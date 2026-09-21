namespace Fookbase.Api.Modules.Identity.DTOs.Responses;

public sealed record AuthenticationResponse(
    AuthenticatedUserResponse User,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
