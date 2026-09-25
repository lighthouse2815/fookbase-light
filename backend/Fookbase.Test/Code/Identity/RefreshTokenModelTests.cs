using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class RefreshTokenModelTests
{
    [Fact]
    public void Refresh_token_uses_navigations_and_default_delete_behavior()
    {
        using var dbContext = new FookbaseDbContext(
            new DbContextOptionsBuilder<FookbaseDbContext>()
                .UseNpgsql("Host=localhost;Database=fookbase_model;Username=fookbase;Password=fookbase")
                .Options);

        var entityType = dbContext.Model.FindEntityType(typeof(RefreshToken))!;
        var sessionId = entityType.FindProperty(nameof(RefreshToken.SessionId))!;

        Assert.False(sessionId.IsNullable);
        Assert.NotNull(entityType.FindNavigation(nameof(RefreshToken.User)));
        Assert.NotNull(entityType.FindNavigation(nameof(RefreshToken.Session)));
        Assert.NotNull(entityType.FindNavigation(nameof(RefreshToken.ReplacedByToken)));

        var foreignKey = Assert.Single(
            entityType.GetForeignKeys(),
            key => key.PrincipalEntityType.ClrType == typeof(AuthSession)
                   && key.Properties.Single().Name == nameof(RefreshToken.SessionId));

        Assert.True(foreignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);

        var replacementForeignKey = Assert.Single(
            entityType.GetForeignKeys(),
            key => key.PrincipalEntityType.ClrType == typeof(RefreshToken));

        Assert.Equal(DeleteBehavior.ClientSetNull, replacementForeignKey.DeleteBehavior);
    }
}
