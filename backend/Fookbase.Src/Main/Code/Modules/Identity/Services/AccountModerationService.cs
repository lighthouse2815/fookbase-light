using Fookbase.Api.Modules.Admin.Entities;
using Fookbase.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class AccountModerationService(FookbaseDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<bool> IsUnavailableAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var state = await dbContext.UserModerationStates.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return state is not null && (state.IsDisabled || state.IsSuspendedAt(timeProvider.GetUtcNow()));
    }
}
