using Fookbase.Api.Modules.Groups.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.Modules.Groups;

public static class DependencyInjection
{
    public static IServiceCollection AddGroupsInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<GroupPostAccessService>();
        services.AddScoped<GroupsService>();
        return services;
    }
}
