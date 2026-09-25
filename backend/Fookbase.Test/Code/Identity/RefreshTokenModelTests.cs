using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class RefreshTokenModelTests
{
    [Fact]
    public void Refresh_token_requires_a_session()
    {
        using var dbContext = new FookbaseDbContext(
            new DbContextOptionsBuilder<FookbaseDbContext>()
                .UseNpgsql("Host=localhost;Database=fookbase_model;Username=fookbase;Password=fookbase")
                .Options);

        var entityType = dbContext.Model.FindEntityType(typeof(RefreshToken))!;
        var sessionId = entityType.FindProperty(nameof(RefreshToken.SessionId))!;

        Assert.False(sessionId.IsNullable);

        var foreignKey = Assert.Single(
            entityType.GetForeignKeys(),
            key => key.PrincipalEntityType.ClrType == typeof(AuthSession)
                   && key.Properties.Single().Name == nameof(RefreshToken.SessionId));

        Assert.True(foreignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }
}
