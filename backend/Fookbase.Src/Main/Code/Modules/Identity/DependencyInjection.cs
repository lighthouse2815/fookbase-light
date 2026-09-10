using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Identity.Data;
using Fookbase.Api.Modules.Identity.Config;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.Modules.Identity;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        string connectionString,
        JwtOptions jwtOptions,
        EmailOptions emailOptions,
        AdminOptions adminOptions)
    {
        jwtOptions.Validate();
        emailOptions.Validate();
        adminOptions.Validate();

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
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddDefaultTokenProviders();

        services.AddSingleton(jwtOptions);
        services.AddSingleton(emailOptions);
        services.AddSingleton(adminOptions);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<AdministrationService>();

        return services;
    }
}
