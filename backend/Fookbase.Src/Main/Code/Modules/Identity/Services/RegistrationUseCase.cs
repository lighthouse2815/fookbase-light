using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Users.Services;
using Fookbase.Api.Persistence;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class RegistrationUseCase(
    AuthenticationService authenticationService,
    UserProfileService userProfileService,
    FookbaseDbContext dbContext)
{
    public async Task<ApplicationResult<AuthenticationResponse>> ExecuteAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await authenticationService.RegisterAsync(request, cancellationToken);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return result;
            }

            var user = result.Value!.User;
            await userProfileService.EnsureCreatedAsync(user.Id, user.Username, cancellationToken);
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
