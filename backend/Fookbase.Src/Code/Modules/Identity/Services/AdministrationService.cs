using Fookbase.Api.Shared.Common;
using Fookbase.Api.Persistence;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class AdministrationService(
    FookbaseDbContext dbContext,
    UserManager<User> userManager)
{
    public Task<int> CountUsersAsync(CancellationToken cancellationToken = default) =>
        dbContext.Users.CountAsync(cancellationToken);

    public Task<int> CountActiveUsersAsync(CancellationToken cancellationToken = default) =>
        dbContext.Users.CountAsync(user => user.IsActive, cancellationToken);

    public async Task<ApplicationResult<PagedResponse<AdminUserResponse>>> SearchUsersAsync(
        string? query,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (offset < 0 || limit is < 1 or > IdentityModuleConstants.Administration.MaximumPageSize)
        {
            return ApplicationResult<PagedResponse<AdminUserResponse>>.Failure(new ApplicationError(
                ErrorCode.InvalidPagination,
                $"Offset must be non-negative and limit must be between 1 and {IdentityModuleConstants.Administration.MaximumPageSize}.",
                ApplicationErrorType.Validation));
        }

        var normalizedQuery = query?.Trim().ToLowerInvariant();
        var users = dbContext.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            users = users.Where(user =>
                user.Email!.ToLower().Contains(normalizedQuery) ||
                user.UserName!.ToLower().Contains(normalizedQuery));
        }

        var total = await users.CountAsync(cancellationToken);
        var items = await users
            .OrderByDescending(user => user.CreatedAt)
            .ThenBy(user => user.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var userIds = items.Select(user => user.Id).ToArray();
        var moderationStates = userIds.Length == 0
            ? new Dictionary<Guid, Fookbase.Api.Modules.Admin.Entities.UserModerationState>()
            : await dbContext.UserModerationStates.AsNoTracking().Where(state => userIds.Contains(state.UserId))
                .ToDictionaryAsync(state => state.UserId, cancellationToken);
        var rolesByUserId = userIds.Length == 0
            ? new Dictionary<Guid, string[]>()
            : (await (
                from userRole in dbContext.UserRoles.AsNoTracking()
                join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userIds.Contains(userRole.UserId)
                orderby role.Name
                select new { userRole.UserId, RoleName = role.Name! })
                .ToListAsync(cancellationToken))
                .GroupBy(item => item.UserId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(item => item.RoleName)
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .ToArray());
        var responses = items
            .Select(user => ToResponse(user, rolesByUserId.GetValueOrDefault(user.Id, []), moderationStates.GetValueOrDefault(user.Id)))
            .ToArray();

        return ApplicationResult<PagedResponse<AdminUserResponse>>.Success(
            new PagedResponse<AdminUserResponse>(responses, offset, limit, total));
    }

    public async Task<ApplicationResult<AdminUserResponse>> UpdateUserStatusAsync(
        Guid actorUserId,
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == userId)
        {
            return ApplicationResult<AdminUserResponse>.Failure(Forbidden(
                "Administrators cannot change their own account status."));
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return ApplicationResult<AdminUserResponse>.Failure(NotFound("The user was not found."));
        }

        if (await userManager.IsInRoleAsync(user, IdentityModuleConstants.Roles.Admin))
        {
            return ApplicationResult<AdminUserResponse>.Failure(Forbidden(
                "Administrator accounts cannot be disabled through this endpoint."));
        }

        if (user.IsActive != isActive)
        {
            if (isActive)
            {
                user.Enable();
            }
            else
            {
                user.Disable();
            }

            var update = await userManager.UpdateAsync(user);
            if (!update.Succeeded)
            {
                return ApplicationResult<AdminUserResponse>.Failure(new ApplicationError(
                    "user_update_failed",
                    "The account status could not be updated.",
                    ApplicationErrorType.Conflict));
            }
        }

        return ApplicationResult<AdminUserResponse>.Success(await ToResponseAsync(user));
    }

    private async Task<AdminUserResponse> ToResponseAsync(User user) =>
        ToResponse(
            user,
            (await userManager.GetRolesAsync(user)).Order(StringComparer.OrdinalIgnoreCase).ToArray(), null);

    private static AdminUserResponse ToResponse(User user, IReadOnlyList<string> roles,
        Fookbase.Api.Modules.Admin.Entities.UserModerationState? state = null) =>
        new(user.Id, user.Email!, user.UserName!, user.IsActive, user.CreatedAt, roles,
            state?.WarningCount ?? 0, state?.SuspendedUntilUtc, state?.DisabledAtUtc);

    private static ApplicationError Forbidden(string message) =>
        new("admin_action_forbidden", message, ApplicationErrorType.Forbidden);

    private static ApplicationError NotFound(string message) =>
        new("user_not_found", message, ApplicationErrorType.NotFound);
}
