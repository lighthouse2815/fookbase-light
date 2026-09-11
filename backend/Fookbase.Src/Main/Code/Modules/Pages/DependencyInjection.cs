using Fookbase.Api.Modules.Pages.Services;
using Microsoft.Extensions.DependencyInjection;

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
