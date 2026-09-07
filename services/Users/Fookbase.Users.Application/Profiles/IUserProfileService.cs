using Fookbase.Users.Application.Common;

namespace Fookbase.Users.Application.Profiles;

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
