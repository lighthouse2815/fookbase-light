using Fookbase.Api.Modules.Users.Domain.Enums;

namespace Fookbase.Api.Modules.Users.Entities;

public sealed class UserProfile
{
    private UserProfile()
    {
    }

    private UserProfile(
        Guid userId,
        string username,
        DateTimeOffset createdAt)
    {
        UserId = userId;
        Username = username;
        DisplayName = username;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid UserId { get; private set; }

    public string Username { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string? Bio { get; private set; }

    public string? AvatarUrl { get; private set; }

    public string? CoverUrl { get; private set; }

    public Guid? AvatarMediaId { get; private set; }

    public Guid? CoverMediaId { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

    public Gender Gender { get; private set; } = Gender.PreferNotToSay;

    public BirthdayVisibility BirthdayVisibility { get; private set; } = BirthdayVisibility.OnlyMe;

    public string? CurrentCity { get; private set; }

    public string? Hometown { get; private set; }

    public string? Workplace { get; private set; }

    public string? Education { get; private set; }

    public string? Website { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static UserProfile Create(
        Guid userId,
        string username,
        DateTimeOffset createdAt) =>
        new(userId, username, createdAt);

    public static UserProfile Create(
        Guid userId,
        string username,
        string displayName,
        DateOnly dateOfBirth,
        Gender gender,
        DateTimeOffset createdAt)
    {
        var profile = new UserProfile(userId, username, createdAt);
        profile.DisplayName = displayName.Trim();
        profile.DateOfBirth = dateOfBirth;
        profile.Gender = gender;
        return profile;
    }

    public void Update(
        string? displayName,
        string? bio,
        DateOnly? dateOfBirth,
        string? currentCity,
        Guid? avatarMediaId,
        Guid? coverMediaId,
        DateTimeOffset updatedAt) =>
        Update(displayName, bio, dateOfBirth, currentCity, avatarMediaId, coverMediaId,
            null, null, null, null, null, updatedAt);

    public void Update(
        string? displayName,
        string? bio,
        DateOnly? dateOfBirth,
        string? currentCity,
        Guid? avatarMediaId,
        Guid? coverMediaId,
        BirthdayVisibility? birthdayVisibility,
        string? hometown,
        string? workplace,
        string? education,
        string? website,
        DateTimeOffset updatedAt)
    {
        if (displayName is not null)
        {
            DisplayName = displayName.Trim();
        }

        if (bio is not null)
        {
            Bio = NormalizeOptionalText(bio);
        }

        if (dateOfBirth is not null)
        {
            DateOfBirth = dateOfBirth;
        }

        if (currentCity is not null)
        {
            CurrentCity = NormalizeOptionalText(currentCity);
        }

        if (birthdayVisibility is not null)
        {
            BirthdayVisibility = birthdayVisibility.Value;
        }

        if (hometown is not null)
        {
            Hometown = NormalizeOptionalText(hometown);
        }

        if (workplace is not null)
        {
            Workplace = NormalizeOptionalText(workplace);
        }

        if (education is not null)
        {
            Education = NormalizeOptionalText(education);
        }

        if (website is not null)
        {
            Website = NormalizeOptionalText(website);
        }

        if (avatarMediaId is not null)
        {
            AvatarMediaId = avatarMediaId;
            AvatarUrl = null;
        }

        if (coverMediaId is not null)
        {
            CoverMediaId = coverMediaId;
            CoverUrl = null;
        }

        UpdatedAt = updatedAt;
    }

    private static string? NormalizeOptionalText(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
