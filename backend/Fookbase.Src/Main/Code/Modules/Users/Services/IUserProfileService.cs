using Fookbase.Api.Modules.Users.Services;

namespace Fookbase.Api.Modules.Users.Services;

public interface IUserProfileService
{
    Task<ApplicationResult<UserProfileResponse>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ApplicationResult<UserProfileResponse>> UpdateAsync(
        Guid userId,
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken = default);
}
