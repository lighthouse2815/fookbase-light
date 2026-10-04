using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class AuthSessionModelTests
{
    [Fact]
    public void Auth_session_requires_an_existing_user()
    {
        using var dbContext = new FookbaseDbContext(
            new DbContextOptionsBuilder<FookbaseDbContext>()
                .UseNpgsql("Host=localhost;Database=fookbase_model;Username=fookbase;Password=fookbase")
                .Options);

        var authSession = dbContext.Model.FindEntityType(typeof(AuthSession))!;
        var foreignKey = Assert.Single(authSession.GetForeignKeys(), key =>
            key.PrincipalEntityType.ClrType == typeof(User));

        Assert.Equal(nameof(AuthSession.UserId), Assert.Single(foreignKey.Properties).Name);
        Assert.True(foreignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }
}
