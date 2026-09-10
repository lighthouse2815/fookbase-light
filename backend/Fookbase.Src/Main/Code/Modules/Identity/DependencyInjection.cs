using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Identity.Config;
using Fookbase.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.Modules.Identity;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        JwtOptions jwtOptions,
        EmailOptions emailOptions,
        AdminOptions adminOptions)
    {
        jwtOptions.Validate();
        emailOptions.Validate();
        adminOptions.Validate();

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
            .AddEntityFrameworkStores<FookbaseDbContext>()
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
