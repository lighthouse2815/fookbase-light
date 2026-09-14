using Fookbase.Api.Modules.Users.Common;
using Fookbase.Api.Persistence;
using Fookbase.Api.Modules.Users.DTOs.Requests;
using Fookbase.Api.Modules.Users.DTOs.Responses;
using Fookbase.Api.Modules.Users.Entities;
using Fookbase.Api.Modules.Friends.Entities;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Photos.Entities;
using Fookbase.Api.Modules.Photos.Services;
using Fookbase.Api.Modules.Posts.Services;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Users.Services;

public sealed class UserProfileService(
    FookbaseDbContext dbContext,
    MediaService mediaService,
    PhotosService photosService,
    PostsService postsService,
    SocialInteractionsService socialInteractionsService,
    UserPrivacySettingsService privacySettingsService,
    TimeProvider timeProvider)
{
    private const int MaximumSearchLimit = 50;
    private const string AvatarUpdatedPostContent = "đã cập nhật ảnh đại diện.";
    private const string CoverUpdatedPostContent = "đã cập nhật ảnh bìa.";

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

    public Task<ApplicationResult<UserProfileResponse>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        GetAsync(userId, null, cancellationToken);

    public async Task<ApplicationResult<UserProfileResponse>> GetAsync(
        Guid userId,
        Guid? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var profile = await ProjectProfiles(viewerUserId)
            .SingleOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

        return profile is null
            ? NotFound()
            : ApplicationResult<UserProfileResponse>.Success(ToResponse(profile, viewerUserId));
    }

    public Task<ApplicationResult<PagedResponse<UserProfileResponse>>> SearchAsync(
        string? query,
        int offset,
        int limit,
        CancellationToken cancellationToken = default) =>
        SearchAsync(query, offset, limit, null, cancellationToken);

    public async Task<ApplicationResult<PagedResponse<UserProfileResponse>>> SearchAsync(
        string? query,
        int offset,
        int limit,
        Guid? viewerUserId,
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
        var profiles = ProjectProfiles(viewerUserId);
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
            .Select(profile => ToResponse(profile, viewerUserId))
            .ToListAsync(cancellationToken);

        return ApplicationResult<PagedResponse<UserProfileResponse>>.Success(
            new PagedResponse<UserProfileResponse>(items, offset, limit, total));
    }

    public async Task<ApplicationResult<UserProfileResponse>> UpdateAsync(
        Guid userId,
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(request, DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime));
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
            var avatarChanged = request.AvatarMediaId is { } requestedAvatarMediaId &&
                requestedAvatarMediaId != profile.AvatarMediaId;
            var coverChanged = request.CoverMediaId is { } requestedCoverMediaId &&
                requestedCoverMediaId != profile.CoverMediaId;
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
                request.BirthdayVisibility,
                request.Hometown,
                request.Workplace,
                request.Education,
                request.Website,
                timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            if (request.AvatarMediaId is not null)
            {
                await photosService.AddSystemMediaAsync(userId, PhotoAlbumType.ProfilePictures, request.AvatarMediaId.Value, cancellationToken);
            }
            if (request.CoverMediaId is not null)
            {
                await photosService.AddSystemMediaAsync(userId, PhotoAlbumType.CoverPhotos, request.CoverMediaId.Value, cancellationToken);
            }
            if (avatarChanged)
            {
                await CreateProfileMediaPostAsync(
                    userId, AvatarUpdatedPostContent, request.AvatarMediaId!.Value, cancellationToken);
            }
            if (coverChanged)
            {
                await CreateProfileMediaPostAsync(
                    userId, CoverUpdatedPostContent, request.CoverMediaId!.Value, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return await GetAsync(userId, userId, cancellationToken);
    }

    private async Task CreateProfileMediaPostAsync(
        Guid userId,
        string content,
        Guid mediaId,
        CancellationToken cancellationToken)
    {
        var privacy = await privacySettingsService.GetDefaultPostPrivacyAsync(userId, cancellationToken);
        var created = await postsService.CreatePostCoreAsync(
            userId, content, privacy, [mediaId], cancellationToken, addToTimelinePhotos: false);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException("The profile media post could not be created.");
        }

        var references = await mediaService.SynchronizePostReferencesAsync(
            userId, created.Value!.Id, [mediaId], cancellationToken);
        if (!references.Succeeded)
        {
            throw new InvalidOperationException("The profile media post references could not be synchronized.");
        }

        await socialInteractionsService.SynchronizePostMetadataAsync(
            created.Value.Id, userId, cancellationToken);
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

    public async Task<IReadOnlyList<BirthdayFriendResponse>> GetTodaysBirthdaysAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var candidates = await GetBirthdayCandidatesAsync(actorUserId, cancellationToken);
        return candidates.Where(candidate => EffectiveBirthday(candidate.DateOfBirth, today.Year) == today)
            .OrderBy(candidate => candidate.DisplayName).Select(ToBirthdayResponse).ToList();
    }

    public async Task<ApplicationResult<IReadOnlyList<BirthdayFriendResponse>>> GetUpcomingBirthdaysAsync(
        Guid actorUserId,
        int days,
        CancellationToken cancellationToken = default)
    {
        if (days is < 1 or > 30)
        {
            return ApplicationResult<IReadOnlyList<BirthdayFriendResponse>>.Failure(
                new ApplicationError("invalid_days", "Days must be between 1 and 30.", ApplicationErrorType.Validation));
        }

        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var end = today.AddDays(days);
        var candidates = await GetBirthdayCandidatesAsync(actorUserId, cancellationToken);
        var matches = candidates.Select(candidate => new { Candidate = candidate, Next = NextBirthday(candidate.DateOfBirth, today) })
            .Where(item => item.Next > today && item.Next <= end).OrderBy(item => item.Next)
            .ThenBy(item => item.Candidate.DisplayName).Select(item => ToBirthdayResponse(item.Candidate)).ToList();
        return ApplicationResult<IReadOnlyList<BirthdayFriendResponse>>.Success(matches);
    }

    private static Dictionary<string, string[]> Validate(
        UpdateUserProfileRequest request,
        DateOnly today)
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

        if (request.Hometown?.Length > 100)
        {
            errors["hometown"] = ["Hometown cannot exceed 100 characters."];
        }

        if (request.Workplace?.Length > 150)
        {
            errors["workplace"] = ["Workplace cannot exceed 150 characters."];
        }

        if (request.Education?.Length > 150)
        {
            errors["education"] = ["Education cannot exceed 150 characters."];
        }

        if (request.Website is { Length: > 2048 } ||
            request.Website is { } website &&
            (!Uri.TryCreate(website.Trim(), UriKind.Absolute, out var uri) ||
             (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            errors["website"] = ["Website must be a valid HTTP or HTTPS URL up to 2048 characters."];
        }

        if (request.BirthdayVisibility is { } visibility && !Enum.IsDefined(visibility))
        {
            errors["birthdayVisibility"] = ["Birthday visibility is invalid."];
        }

        if (request.DateOfBirth > today)
        {
            errors["dateOfBirth"] = ["Date of birth cannot be in the future."];
        }

        return errors;
    }

    private async Task<IReadOnlyList<BirthdayCandidate>> GetBirthdayCandidatesAsync(Guid actorUserId, CancellationToken cancellationToken)
    {
        var friendUserIds = dbContext.Friendships.AsNoTracking()
            .Where(friendship => friendship.UserId1 == actorUserId || friendship.UserId2 == actorUserId)
            .Select(friendship => friendship.UserId1 == actorUserId ? friendship.UserId2 : friendship.UserId1);

        return await (from profile in dbContext.UserProfiles.AsNoTracking()
                      join user in dbContext.Users.AsNoTracking() on profile.UserId equals user.Id
                      where friendUserIds.Contains(profile.UserId) && user.IsActive && profile.DateOfBirth != null &&
                            profile.BirthdayVisibility != BirthdayVisibility.OnlyMe &&
                            !dbContext.BlockedUsers.AsNoTracking().Any(block =>
                                (block.BlockerUserId == actorUserId && block.BlockedUserId == profile.UserId) ||
                                (block.BlockerUserId == profile.UserId && block.BlockedUserId == actorUserId))
                      select new BirthdayCandidate(profile.UserId, profile.Username, profile.DisplayName,
                          profile.AvatarMediaId == null ? profile.AvatarUrl : $"/api/users/{profile.UserId}/avatar",
                          profile.DateOfBirth!.Value)).ToListAsync(cancellationToken);
    }

    private static BirthdayFriendResponse ToBirthdayResponse(BirthdayCandidate candidate) =>
        new(candidate.UserId, candidate.Username, candidate.DisplayName, candidate.AvatarUrl,
            candidate.DateOfBirth.Month, candidate.DateOfBirth.Day);

    private static DateOnly NextBirthday(DateOnly birthDate, DateOnly today)
    {
        var occurrence = EffectiveBirthday(birthDate, today.Year);
        return occurrence < today ? EffectiveBirthday(birthDate, today.Year + 1) : occurrence;
    }

    private static DateOnly EffectiveBirthday(DateOnly birthDate, int year) =>
        new(year, birthDate.Month, Math.Min(birthDate.Day, DateTime.DaysInMonth(year, birthDate.Month)));

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

    private IQueryable<UserProfileProjection> ProjectProfiles(Guid? viewerUserId)
    {
        return dbContext.UserProfiles.AsNoTracking()
            .Where(profile => viewerUserId == null ||
                !dbContext.BlockedUsers.AsNoTracking().Any(block =>
                    (block.BlockerUserId == viewerUserId && block.BlockedUserId == profile.UserId) ||
                    (block.BlockerUserId == profile.UserId && block.BlockedUserId == viewerUserId)))
            .Select(profile => new UserProfileProjection
            {
                UserId = profile.UserId,
                Username = profile.Username,
                DisplayName = profile.DisplayName,
                Bio = profile.Bio,
                AvatarUrl = profile.AvatarUrl,
                CoverUrl = profile.CoverUrl,
                AvatarMediaId = profile.AvatarMediaId,
                CoverMediaId = profile.CoverMediaId,
                DateOfBirth = profile.DateOfBirth,
                BirthdayVisibility = profile.BirthdayVisibility,
                CurrentCity = profile.CurrentCity,
                Hometown = profile.Hometown,
                Workplace = profile.Workplace,
                Education = profile.Education,
                Website = profile.Website,
                CreatedAt = profile.CreatedAt,
                UpdatedAt = profile.UpdatedAt,
                FollowerCount = dbContext.UserFollows.AsNoTracking().Count(follow =>
                    follow.FollowingUserId == profile.UserId &&
                    dbContext.Users.Any(user => user.Id == follow.FollowerUserId && user.IsActive) &&
                    dbContext.UserProfiles.Any(other => other.UserId == follow.FollowerUserId) &&
                    !dbContext.BlockedUsers.AsNoTracking().Any(block =>
                        (block.BlockerUserId == follow.FollowerUserId && block.BlockedUserId == profile.UserId) ||
                        (block.BlockerUserId == profile.UserId && block.BlockedUserId == follow.FollowerUserId)) &&
                    (viewerUserId == null || !dbContext.BlockedUsers.AsNoTracking().Any(block =>
                        (block.BlockerUserId == viewerUserId && block.BlockedUserId == follow.FollowerUserId) ||
                        (block.BlockerUserId == follow.FollowerUserId && block.BlockedUserId == viewerUserId)))),
                FollowingCount = dbContext.UserFollows.AsNoTracking().Count(follow =>
                    follow.FollowerUserId == profile.UserId &&
                    dbContext.Users.Any(user => user.Id == follow.FollowingUserId && user.IsActive) &&
                    dbContext.UserProfiles.Any(other => other.UserId == follow.FollowingUserId) &&
                    !dbContext.BlockedUsers.AsNoTracking().Any(block =>
                        (block.BlockerUserId == profile.UserId && block.BlockedUserId == follow.FollowingUserId) ||
                        (block.BlockerUserId == follow.FollowingUserId && block.BlockedUserId == profile.UserId)) &&
                    (viewerUserId == null || !dbContext.BlockedUsers.AsNoTracking().Any(block =>
                        (block.BlockerUserId == viewerUserId && block.BlockedUserId == follow.FollowingUserId) ||
                        (block.BlockerUserId == follow.FollowingUserId && block.BlockedUserId == viewerUserId)))),
                IsFollowing = viewerUserId == null ? null : dbContext.UserFollows.AsNoTracking().Any(follow =>
                    follow.FollowerUserId == viewerUserId && follow.FollowingUserId == profile.UserId),
                IsFollowedBy = viewerUserId == null ? null : dbContext.UserFollows.AsNoTracking().Any(follow =>
                    follow.FollowerUserId == profile.UserId && follow.FollowingUserId == viewerUserId),
                FriendshipState = viewerUserId == null ? null :
                    profile.UserId == viewerUserId ? "self" :
                    dbContext.Friendships.AsNoTracking().Any(friendship =>
                        (friendship.UserId1 == viewerUserId && friendship.UserId2 == profile.UserId) ||
                        (friendship.UserId1 == profile.UserId && friendship.UserId2 == viewerUserId)) ? "friends" :
                    dbContext.FriendRequests.AsNoTracking().Any(request =>
                        request.SenderUserId == viewerUserId && request.ReceiverUserId == profile.UserId &&
                        request.Status == FriendRequestStatus.Pending) ? "request_sent" :
                    dbContext.FriendRequests.AsNoTracking().Any(request =>
                        request.SenderUserId == profile.UserId && request.ReceiverUserId == viewerUserId &&
                        request.Status == FriendRequestStatus.Pending) ? "request_received" : "none"
            });
    }

    private static UserProfileResponse ToResponse(UserProfileProjection profile, Guid? viewerUserId)
    {
        var isOwner = viewerUserId == profile.UserId;
        var canViewBirthday = profile.DateOfBirth is not null &&
            (isOwner || viewerUserId is not null &&
             (profile.BirthdayVisibility == BirthdayVisibility.Public ||
              profile.BirthdayVisibility == BirthdayVisibility.Friends && profile.FriendshipState == "friends"));

        return new UserProfileResponse(
            profile.UserId,
            profile.Username,
            profile.DisplayName,
            profile.Bio,
            profile.AvatarMediaId is null ? profile.AvatarUrl : $"/api/users/{profile.UserId}/avatar",
            profile.CoverMediaId is null ? profile.CoverUrl : $"/api/users/{profile.UserId}/cover",
            isOwner ? profile.DateOfBirth : null,
            profile.CurrentCity,
            profile.CreatedAt,
            profile.UpdatedAt,
            profile.FollowerCount,
            profile.FollowingCount,
            profile.IsFollowing,
            profile.IsFollowedBy,
            profile.FriendshipState,
            canViewBirthday ? new BirthdayResponse(profile.DateOfBirth!.Value.Month, profile.DateOfBirth.Value.Day) : null,
            isOwner ? profile.BirthdayVisibility.ToString().ToLowerInvariant() : null,
            profile.Hometown,
            profile.Workplace,
            profile.Education,
            profile.Website);
    }

    private sealed class UserProfileProjection
    {
        public Guid UserId { get; init; }
        public string Username { get; init; } = null!;
        public string DisplayName { get; init; } = null!;
        public string? Bio { get; init; }
        public string? AvatarUrl { get; init; }
        public string? CoverUrl { get; init; }
        public Guid? AvatarMediaId { get; init; }
        public Guid? CoverMediaId { get; init; }
        public DateOnly? DateOfBirth { get; init; }
        public BirthdayVisibility BirthdayVisibility { get; init; }
        public string? CurrentCity { get; init; }
        public string? Hometown { get; init; }
        public string? Workplace { get; init; }
        public string? Education { get; init; }
        public string? Website { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
        public int FollowerCount { get; init; }
        public int FollowingCount { get; init; }
        public bool? IsFollowing { get; init; }
        public bool? IsFollowedBy { get; init; }
        public string? FriendshipState { get; init; }
    }

    private sealed record BirthdayCandidate(Guid UserId, string Username, string DisplayName, string? AvatarUrl, DateOnly DateOfBirth);
}
