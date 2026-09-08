namespace Fookbase.Api.Modules.Users.Models;

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

    public DateOnly? DateOfBirth { get; private set; }

    public string? CurrentCity { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static UserProfile Create(
        Guid userId,
        string username,
        DateTimeOffset createdAt) =>
        new(userId, username, createdAt);

    public void Update(
        string? displayName,
        string? bio,
        DateOnly? dateOfBirth,
        string? currentCity,
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

        UpdatedAt = updatedAt;
    }

    private static string? NormalizeOptionalText(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
