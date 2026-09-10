using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Users.Services;

namespace Fookbase.Api.Application;

public sealed class RegistrationUseCase(
    AuthenticationService authenticationService,
    UserProfileService userProfileService)
{
    public async Task<ApplicationResult<AuthenticationResponse>> ExecuteAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await authenticationService.RegisterAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return result;
        }

        var user = result.Value!.User;
        await userProfileService.EnsureCreatedAsync(user.Id, user.Username, cancellationToken);
        return result;
    }
}
