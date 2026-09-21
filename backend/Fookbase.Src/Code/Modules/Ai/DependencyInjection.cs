using System.Net.Http.Headers;
using Fookbase.Api.Modules.Ai.Config;
using Fookbase.Api.Modules.Ai.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.Modules.Ai;

public static class DependencyInjection
{
    public static IServiceCollection AddAiInfrastructure(this IServiceCollection services, AiChatOptions options)
    {
        services.AddSingleton(options);
        services.AddHttpClient<AiChatService>((serviceProvider, client) =>
        {
            var chatOptions = serviceProvider.GetRequiredService<AiChatOptions>();
            client.BaseAddress = new Uri(chatOptions.ApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(chatOptions.RequestTimeoutSeconds);
            if (chatOptions.Enabled)
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", chatOptions.ApiKey);
            }
        });
        return services;
    }
}
