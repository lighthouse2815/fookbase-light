namespace Fookbase.Api.Modules.Identity.DTOs.Responses;

public sealed record BrowserAuthenticationResponse(
    AuthenticatedUserResponse User,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt);
