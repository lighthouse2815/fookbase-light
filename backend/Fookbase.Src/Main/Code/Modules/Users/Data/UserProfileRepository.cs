using Fookbase.Api.Modules.Users.Services;
using Fookbase.Api.Modules.Users.Models;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Users.Data;

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
