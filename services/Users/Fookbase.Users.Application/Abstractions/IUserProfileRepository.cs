using Fookbase.Users.Domain.Entities;

namespace Fookbase.Users.Application.Abstractions;

public interface IUserProfileRepository
{
    Task<UserProfile?> FindByIdAsync(
        Guid userId,
        bool trackChanges = false,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
