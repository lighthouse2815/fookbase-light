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
        AdminOptions adminOptions,
        GoogleAuthenticationOptions googleAuthenticationOptions,
        SmsOptions smsOptions)
    {
        jwtOptions.Validate();
        emailOptions.Validate();
        adminOptions.Validate();
        googleAuthenticationOptions.Validate(production: false);
        smsOptions.Validate(production: false);

        services
            .AddIdentityCore<User>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
                options.User.RequireUniqueEmail = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<FookbaseDbContext>()
            .AddDefaultTokenProviders();

        services.AddSingleton(jwtOptions);
        services.AddSingleton(emailOptions);
        services.AddSingleton(adminOptions);
        services.AddSingleton(googleAuthenticationOptions);
        services.AddSingleton(smsOptions);
        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient<SpeedSmsSender>((provider, client) =>
        {
            var options = provider.GetRequiredService<SmsOptions>();
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IContactOtpSender, ContactOtpSender>();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<GoogleAuthenticationService>();
        services.AddScoped<IGoogleExternalIdentityReader, GoogleExternalIdentityReader>();
        services.AddScoped<AccountModerationService>();
        services.AddScoped<RegistrationUseCase>();
        services.AddScoped<RegistrationChallengeService>();
        services.AddScoped<AdministrationService>();

        return services;
    }
}
