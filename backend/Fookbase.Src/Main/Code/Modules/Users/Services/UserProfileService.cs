using Fookbase.Api.Modules.Users.Common;
using Fookbase.Api.Modules.Users.Data;
using Fookbase.Api.Modules.Users.DTOs.Requests;
using Fookbase.Api.Modules.Users.DTOs.Responses;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Users.Services;

public sealed class UserProfileService(
    UsersDbContext dbContext,
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

        profile.Update(
            request.DisplayName,
            request.Bio,
            request.DateOfBirth,
            request.CurrentCity,
            timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<UserProfileResponse>.Success(ToResponse(profile));
    }

    private static IReadOnlyDictionary<string, string[]> Validate(
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
            profile.AvatarUrl,
            profile.CoverUrl,
            profile.DateOfBirth,
            profile.CurrentCity,
            profile.CreatedAt,
            profile.UpdatedAt);
}
