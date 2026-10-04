using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Modules.Users.Domain.Enums;
using Fookbase.Api.Modules.Users.DTOs.Requests;
using Fookbase.Api.Modules.Users.DTOs.Responses;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Users.Services;

public sealed class UserPrivacySettingsService(FookbaseDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<UserPrivacySettings> EnsureCreatedAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.UserPrivacySettings.SingleOrDefaultAsync(
            item => item.UserId == userId,
            cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        settings = new UserPrivacySettings(userId, timeProvider.GetUtcNow());
        dbContext.UserPrivacySettings.Add(settings);
        await dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task<ApplicationResult<UserPrivacySettingsResponse>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        ApplicationResult<UserPrivacySettingsResponse>.Success(ToResponse(
            await EnsureCreatedAsync(userId, cancellationToken)));

    public async Task<ApplicationResult<UserPrivacySettingsResponse>> UpdateAsync(
        Guid userId,
        UpdatePrivacySettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseOptional(request.DefaultPostPrivacy, out PostPrivacy? postPrivacy) ||
            !TryParseOptional(request.FriendRequestPolicy, out FriendRequestPolicy? requestPolicy) ||
            !TryParseOptional(request.FriendListVisibility, out RelationshipListVisibility? friendVisibility) ||
            !TryParseOptional(request.FollowListVisibility, out RelationshipListVisibility? followVisibility))
        {
            return ApplicationResult<UserPrivacySettingsResponse>.Failure(new ApplicationError(
                "invalid_privacy_setting",
                "Privacy settings contain an unsupported value.",
                ApplicationErrorType.VALIDATION));
        }

        var settings = await EnsureCreatedAsync(userId, cancellationToken);
        settings.Update(postPrivacy, requestPolicy, friendVisibility, followVisibility, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult<UserPrivacySettingsResponse>.Success(ToResponse(settings));
    }

    public async Task<PostPrivacy> GetDefaultPostPrivacyAsync(Guid userId, CancellationToken cancellationToken = default) =>
        (await EnsureCreatedAsync(userId, cancellationToken)).DefaultPostPrivacy;

    private static bool TryParseOptional<TEnum>(string? value, out TEnum? parsed) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = null;
            return true;
        }

        var success = EnumText.TryParse(value, true, out TEnum candidate) && Enum.IsDefined(candidate);
        parsed = success ? candidate : null;
        return success;
    }

    private static UserPrivacySettingsResponse ToResponse(UserPrivacySettings settings) => new(
        ToCamelCase(settings.DefaultPostPrivacy),
        ToCamelCase(settings.FriendRequestPolicy),
        ToCamelCase(settings.FriendListVisibility),
        ToCamelCase(settings.FollowListVisibility),
        settings.UpdatedAtUtc);

    private static string ToCamelCase<TEnum>(TEnum value) where TEnum : struct, Enum =>
        char.ToLowerInvariant(value.ToApiName()[0]) + value.ToApiName()[1..];
}
