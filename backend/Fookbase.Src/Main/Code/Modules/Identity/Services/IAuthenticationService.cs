using Fookbase.Api.Modules.Identity.Services;

namespace Fookbase.Api.Modules.Identity.Services;

public interface IAuthenticationService
{
    Task<ApplicationResult<AuthenticationResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<ApplicationResult<AuthenticationResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<ApplicationResult<AuthenticationResponse>> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default);

    Task<ApplicationResult> LogoutAsync(
        Guid userId,
        LogoutRequest request,
        CancellationToken cancellationToken = default);

    Task<ApplicationResult<AuthenticatedUserResponse>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
