using Fookbase.Api.Modules.Users.Models;

namespace Fookbase.Api.Modules.Users.Services;

public interface IUserProfileRepository
{
    Task<UserProfile?> FindByIdAsync(
        Guid userId,
        bool trackChanges = false,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
