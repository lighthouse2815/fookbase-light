using Fookbase.Api.Modules.Admin.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.Modules.Admin;

public static class DependencyInjection
{
    public static IServiceCollection AddAdminInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<AdministrationUseCase>();
        services.AddScoped<ModerationService>();

        return services;
    }
}
