using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Users.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Users.Entities;

[Index(nameof(Username), IsUnique = true)]
public sealed class UserProfile
{
    private UserProfile(){}

    public UserProfile(
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

    public UserProfile(
        Guid userId,
        string username,
        string displayName,
        DateOnly dateOfBirth,
        Gender gender,
        DateTimeOffset createdAt)
        : this(userId, username, createdAt)
    {
        DisplayName = displayName.Trim();
        DateOfBirth = dateOfBirth;
        Gender = gender;
    }

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid UserId { get; private set; }

    [Required]
    [MaxLength(32)]
    public string Username { get; private set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; private set; } = string.Empty;

    [MaxLength(500)]
    public string? Bio { get; private set; }

    [MaxLength(2048)]
    public string? AvatarUrl { get; private set; }

    [MaxLength(2048)]
    public string? CoverUrl { get; private set; }

    public Guid? AvatarMediaId { get; private set; }

    public Guid? CoverMediaId { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

    [DefaultValue(Gender.PREFER_NOT_TO_SAY)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Gender Gender { get; private set; } = Gender.PREFER_NOT_TO_SAY;

    [DefaultValue(BirthdayVisibility.ONLY_ME)]
    public BirthdayVisibility BirthdayVisibility { get; private set; } = BirthdayVisibility.ONLY_ME;

    [MaxLength(100)]
    public string? CurrentCity { get; private set; }

    [MaxLength(100)]
    public string? Hometown { get; private set; }

    [MaxLength(150)]
    public string? Workplace { get; private set; }

    [MaxLength(150)]
    public string? Education { get; private set; }

    [MaxLength(2048)]
    public string? Website { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    [ForeignKey(nameof(UserId))]
    [InverseProperty(nameof(User.Profile))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public User User { get; private set; } = null!;

    [ForeignKey(nameof(AvatarMediaId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset? AvatarMedia { get; private set; }

    [ForeignKey(nameof(CoverMediaId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset? CoverMedia { get; private set; }

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
