using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Users.Domain.Enums;
using Fookbase.Api.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Fookbase.Users.Api.IntegrationTests;

public sealed class UserProfileModelTests
{
    [Fact]
    public void Profile_shares_its_key_with_one_required_user()
    {
        using var dbContext = CreateDbContext();
        var profile = dbContext.Model.FindEntityType(typeof(UserProfile))!;
        var foreignKey = Assert.Single(profile.GetForeignKeys(), key =>
            key.PrincipalEntityType.ClrType == typeof(User));

        Assert.Equal(profile.FindPrimaryKey()!.Properties, foreignKey.Properties);
        Assert.Equal(nameof(UserProfile.User), foreignKey.DependentToPrincipal!.Name);
        Assert.Equal(nameof(User.Profile), foreignKey.PrincipalToDependent!.Name);
        Assert.True(foreignKey.IsRequired);
        Assert.True(foreignKey.IsUnique);
        Assert.False(foreignKey.IsRequiredDependent);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        Assert.Equal(ValueGenerated.Never, profile.FindProperty(nameof(UserProfile.UserId))!.ValueGenerated);
        Assert.Equal(ValueGenerated.Never, profile.FindProperty(nameof(UserProfile.Gender))!.ValueGenerated);
    }

    [Fact]
    public void Profile_preserves_database_defaults_declared_on_the_entity()
    {
        using var dbContext = CreateDbContext();
        var profile = dbContext.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(UserProfile))!;
        var gender = profile.FindProperty(nameof(UserProfile.Gender))!;
        var birthdayVisibility = profile.FindProperty(nameof(UserProfile.BirthdayVisibility))!;

        Assert.Equal(Gender.PREFER_NOT_TO_SAY, gender.GetDefaultValue());
        Assert.Equal(ValueGenerated.Never, gender.ValueGenerated);
        Assert.Equal(BirthdayVisibility.ONLY_ME, birthdayVisibility.GetDefaultValue());
        Assert.Equal(ValueGenerated.OnAdd, birthdayVisibility.ValueGenerated);
    }

    [Theory]
    [InlineData(nameof(UserProfile.AvatarMedia), nameof(UserProfile.AvatarMediaId))]
    [InlineData(nameof(UserProfile.CoverMedia), nameof(UserProfile.CoverMediaId))]
    public void Profile_media_is_optional_and_cannot_delete_the_profile(string navigationName, string propertyName)
    {
        using var dbContext = CreateDbContext();
        var profile = dbContext.Model.FindEntityType(typeof(UserProfile))!;
        var foreignKey = profile.FindNavigation(navigationName)!.ForeignKey;

        Assert.Equal(typeof(MediaAsset), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(propertyName, Assert.Single(foreignKey.Properties).Name);
        Assert.False(foreignKey.IsRequired);
        Assert.False(foreignKey.IsUnique);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(3, profile.GetForeignKeys().Count());
    }

    private static FookbaseDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<FookbaseDbContext>()
            .UseNpgsql("Host=localhost;Database=fookbase_model;Username=fookbase;Password=fookbase")
            .Options);
}
