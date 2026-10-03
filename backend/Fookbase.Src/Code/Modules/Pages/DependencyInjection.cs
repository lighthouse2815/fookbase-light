using Fookbase.Api.Modules.Pages.Services;

namespace Fookbase.Api.Modules.Pages;

public static class DependencyInjection
{
    public static IServiceCollection AddPagesInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<PagePostAccessService>();
        services.AddScoped<PagesService>();
        return services;
    }
}
