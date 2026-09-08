using Fookbase.Users.Application.Abstractions;
using Fookbase.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Users.Infrastructure.Persistence;

internal sealed class UserProfileRepository(UsersDbContext dbContext)
    : IUserProfileRepository
{
    public Task<UserProfile?> FindByIdAsync(
        Guid userId,
        bool trackChanges = false,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.UserProfiles.AsQueryable();
        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(
            profile => profile.UserId == userId,
            cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
