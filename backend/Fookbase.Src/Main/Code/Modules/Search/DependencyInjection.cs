using Fookbase.Api.Modules.Search.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.Modules.Search;

public static class DependencyInjection
{
    public static IServiceCollection AddSearchInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<SearchService>();
        return services;
    }
}
