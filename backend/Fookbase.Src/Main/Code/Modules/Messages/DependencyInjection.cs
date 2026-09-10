using Fookbase.Api.Modules.Messages.Data;
using Fookbase.Api.Modules.Messages.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR;
using Fookbase.Api.Modules.Messages.Hubs;

namespace Fookbase.Api.Modules.Messages;

public static class DependencyInjection
{
    public static IServiceCollection AddMessagesInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<MessagesDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
        services.AddScoped<MessagesService>();

        return services;
    }
}
