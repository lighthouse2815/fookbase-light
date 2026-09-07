using Fookbase.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString));

        services
            .AddIdentityCore<IdentityUser<Guid>>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityDbContext>();

        return services;
    }
}
