using Fookbase.Api.Modules.Users.Common;
using Fookbase.Api.Persistence;
using Fookbase.Api.Modules.Users.DTOs.Requests;
using Fookbase.Api.Modules.Users.DTOs.Responses;
using Fookbase.Api.Modules.Users.Entities;
using Fookbase.Api.Modules.Media.Services;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Users.Services;

public sealed class UserProfileService(
    FookbaseDbContext dbContext,
    MediaService mediaService,
    TimeProvider timeProvider)
{
    private const int MaximumSearchLimit = 50;

    public async Task EnsureCreatedAsync(
        Guid userId,
        string username,
        CancellationToken cancellationToken = default)
    {
        if (await dbContext.UserProfiles.AnyAsync(profile => profile.UserId == userId, cancellationToken))
        {
            return;
        }

        dbContext.UserProfiles.Add(UserProfile.Create(userId, username, timeProvider.GetUtcNow()));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            if (!await dbContext.UserProfiles.AnyAsync(profile => profile.UserId == userId, cancellationToken))
            {
                throw;
            }
        }
    }

    public async Task<ApplicationResult<UserProfileResponse>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.UserProfiles.AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

        return profile is null
            ? NotFound()
            : ApplicationResult<UserProfileResponse>.Success(ToResponse(profile));
    }

    public async Task<ApplicationResult<PagedResponse<UserProfileResponse>>> SearchAsync(
        string? query,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (offset < 0 || limit is < 1 or > MaximumSearchLimit)
        {
            return ApplicationResult<PagedResponse<UserProfileResponse>>.Failure(
                new ApplicationError(
                    "invalid_pagination",
                    $"Offset must be non-negative and limit must be between 1 and {MaximumSearchLimit}.",
                    ApplicationErrorType.Validation));
        }

        var normalizedQuery = query?.Trim().ToLowerInvariant();
        var profiles = dbContext.UserProfiles.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            profiles = profiles.Where(profile =>
                profile.Username.ToLower().Contains(normalizedQuery) ||
                profile.DisplayName.ToLower().Contains(normalizedQuery));
        }

        var total = await profiles.CountAsync(cancellationToken);
        var items = await profiles
            .OrderBy(profile => profile.Username)
            .Skip(offset)
            .Take(limit)
            .Select(profile => ToResponse(profile))
            .ToListAsync(cancellationToken);

        return ApplicationResult<PagedResponse<UserProfileResponse>>.Success(
            new PagedResponse<UserProfileResponse>(items, offset, limit, total));
    }

    public async Task<ApplicationResult<UserProfileResponse>> UpdateAsync(
        Guid userId,
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request, timeProvider.GetUtcNow());
        await ValidateProfileMediaAsync(userId, request, errors, cancellationToken);
        if (errors.Count > 0)
        {
            return ApplicationResult<UserProfileResponse>.Failure(
                new ApplicationError(
                    "validation_failed",
                    "One or more validation errors occurred.",
                    ApplicationErrorType.Validation,
                    errors));
        }

        var profile = await dbContext.UserProfiles
            .SingleOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var avatarMediaId = request.AvatarMediaId ?? profile.AvatarMediaId;
            var coverMediaId = request.CoverMediaId ?? profile.CoverMediaId;
            await mediaService.SynchronizeProfileReferencesAsync(
                userId,
                avatarMediaId,
                coverMediaId,
                cancellationToken);
            profile.Update(
                request.DisplayName,
                request.Bio,
                request.DateOfBirth,
                request.CurrentCity,
                request.AvatarMediaId,
                request.CoverMediaId,
                timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return ApplicationResult<UserProfileResponse>.Success(ToResponse(profile));
    }

    public async Task<Guid?> GetAvatarMediaIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await dbContext.UserProfiles.AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => profile.AvatarMediaId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<Guid?> GetCoverMediaIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await dbContext.UserProfiles.AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => profile.CoverMediaId)
            .SingleOrDefaultAsync(cancellationToken);

    private static Dictionary<string, string[]> Validate(
        UpdateUserProfileRequest request,
        DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (request.DisplayName is not null &&
            request.DisplayName.Trim().Length is < 1 or > 100)
        {
            errors["displayName"] = ["Display name must contain between 1 and 100 characters."];
        }

        if (request.Bio?.Length > 500)
        {
            errors["bio"] = ["Bio cannot exceed 500 characters."];
        }

        if (request.CurrentCity?.Length > 100)
        {
            errors["currentCity"] = ["Current city cannot exceed 100 characters."];
        }

        if (request.DateOfBirth > DateOnly.FromDateTime(now.UtcDateTime))
        {
            errors["dateOfBirth"] = ["Date of birth cannot be in the future."];
        }

        return errors;
    }

    private async Task ValidateProfileMediaAsync(
        Guid userId,
        UpdateUserProfileRequest request,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        if (request.AvatarMediaId is not null)
        {
            var validation = await mediaService.ValidateProfileImageAsync(
                userId,
                request.AvatarMediaId.Value,
                cancellationToken);
            if (!validation.Succeeded)
            {
                errors["avatarMediaId"] = [validation.Error!.Message];
            }
        }

        if (request.CoverMediaId is not null)
        {
            var validation = await mediaService.ValidateProfileImageAsync(
                userId,
                request.CoverMediaId.Value,
                cancellationToken);
            if (!validation.Succeeded)
            {
                errors["coverMediaId"] = [validation.Error!.Message];
            }
        }
    }

    private static ApplicationResult<UserProfileResponse> NotFound() =>
        ApplicationResult<UserProfileResponse>.Failure(
            new ApplicationError(
                "profile_not_found",
                "The user profile was not found.",
                ApplicationErrorType.NotFound));

    private static UserProfileResponse ToResponse(UserProfile profile) =>
        new(
            profile.UserId,
            profile.Username,
            profile.DisplayName,
            profile.Bio,
            profile.AvatarMediaId is null ? profile.AvatarUrl : $"/api/users/{profile.UserId}/avatar",
            profile.CoverMediaId is null ? profile.CoverUrl : $"/api/users/{profile.UserId}/cover",
            profile.DateOfBirth,
            profile.CurrentCity,
            profile.CreatedAt,
            profile.UpdatedAt);
}
