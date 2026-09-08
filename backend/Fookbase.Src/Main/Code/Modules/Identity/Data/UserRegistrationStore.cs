using System.Text.Json;
using Fookbase.Api.Shared.Contracts.Identity;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Identity.Models;
using Fookbase.Api.Modules.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Identity.Data;

internal sealed class UserRegistrationStore(
    UserManager<User> userManager,
    IdentityDbContext dbContext) : IUserRegistrationStore
{
    public async Task<UserCreationResult> CreateAsync(
        User user,
        string password,
        RefreshToken refreshToken,
        UserRegisteredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);
        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return ToCreationResult(result);
        }

        dbContext.RefreshTokens.Add(refreshToken);
        dbContext.OutboxMessages.Add(
            OutboxMessage.Create(
                integrationEvent.EventId,
                UserRegisteredIntegrationEvent.EventType,
                JsonSerializer.Serialize(integrationEvent),
                integrationEvent.OccurredAtUtc));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToCreationResult(result);
    }

    private static UserCreationResult ToCreationResult(IdentityResult result)
    {
        var errors = result.Errors
            .GroupBy(error => error.Code, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray(),
                StringComparer.Ordinal);

        return new UserCreationResult(result.Succeeded, errors);
    }
}
