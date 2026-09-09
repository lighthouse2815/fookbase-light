using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Identity.Data;
using Fookbase.Api.Modules.Identity.Messaging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.Modules.Identity.Config;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        string connectionString,
        JwtOptions jwtOptions,
        OutboxOptions outboxOptions)
    {
        jwtOptions.Validate();
        outboxOptions.Validate();

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString));

        services
            .AddIdentityCore<User>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityDbContext>();

        services.AddSingleton(jwtOptions);
        services.AddSingleton(outboxOptions);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<JwtTokenService>();
        services.AddScoped<AuthenticationService>();
        services.AddHostedService<OutboxPublisherWorker>();

        return services;
    }
}
