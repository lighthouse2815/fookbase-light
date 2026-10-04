using Fookbase.Api.Modules.Admin.Services;

namespace Fookbase.Api.Modules.Admin;

public static class DependencyInjection
{
    public static IServiceCollection AddAdminInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<AccountModerationService>();
        services.AddScoped<AdministrationService>();
        services.AddScoped<AdministrationUseCase>();
        services.AddScoped<ModerationService>();

        return services;
    }
}
