using Fookbase.Api.Modules.Users.DTOs;
using Fookbase.Api.Modules.Users.Services;
using Fookbase.Api.Modules.Users.Entities;

namespace Fookbase.Api.Modules.Users.Services;

public sealed class UserProfileService(
    IUserProfileRepository repository,
    TimeProvider timeProvider) : IUserProfileService
{
    public async Task<ApplicationResult<UserProfileResponse>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await repository.FindByIdAsync(
            userId,
            cancellationToken: cancellationToken);

        return profile is null
            ? NotFound()
            : ApplicationResult<UserProfileResponse>.Success(ToResponse(profile));
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

        var profile = await repository.FindByIdAsync(
            userId,
            trackChanges: true,
            cancellationToken);
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
        await repository.SaveChangesAsync(cancellationToken);

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
