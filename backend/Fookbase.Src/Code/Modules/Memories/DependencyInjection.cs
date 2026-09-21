using Fookbase.Api.Modules.Memories.Services;

namespace Fookbase.Api.Modules.Memories;

public static class DependencyInjection
{
    public static IServiceCollection AddMemoriesInfrastructure(this IServiceCollection services) => services.AddScoped<MemoriesService>();
}
