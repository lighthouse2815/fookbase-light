using Fookbase.Api.Modules.Users.Entities;

namespace Fookbase.Api.Modules.Users.Services.Abstractions;

public interface IUserProfileRepository
{
    Task<UserProfile?> FindByIdAsync(
        Guid userId,
        bool trackChanges = false,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
