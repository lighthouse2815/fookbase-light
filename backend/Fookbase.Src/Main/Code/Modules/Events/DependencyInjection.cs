using Fookbase.Api.Modules.Events.Services;
using Microsoft.Extensions.DependencyInjection;
namespace Fookbase.Api.Modules.Events;
public static class DependencyInjection
{ public static IServiceCollection AddEventsInfrastructure(this IServiceCollection services) { services.AddScoped<EventAccessService>(); services.AddScoped<EventsService>(); return services; } }
