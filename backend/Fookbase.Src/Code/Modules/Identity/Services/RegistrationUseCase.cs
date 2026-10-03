using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Modules.Users.Services;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class RegistrationUseCase(
    AuthenticationService authenticationService,
    UserProfileService userProfileService,
    UserPrivacySettingsService privacySettingsService,
    FookbaseDbContext dbContext)
{
    public async Task<AuthenticationResponse> ExecuteAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await authenticationService.RegisterAsync(request, cancellationToken);

            var user = result.User;
            await userProfileService.EnsureCreatedAsync(user.Id, user.Username, cancellationToken);
            await privacySettingsService.EnsureCreatedAsync(user.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
