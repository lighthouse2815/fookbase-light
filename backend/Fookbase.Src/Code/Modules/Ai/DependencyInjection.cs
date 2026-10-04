using System.Net.Http.Headers;
using Fookbase.Api.Modules.Ai.Config;
using Fookbase.Api.Modules.Ai.Services;

namespace Fookbase.Api.Modules.Ai;

public static class DependencyInjection
{
    public static IServiceCollection AddAiInfrastructure(this IServiceCollection services, AiChatOptions options)
    {
        services.AddSingleton(options);
        services.AddHttpClient();
        foreach (var provider in options.GetConfiguredProviders())
        {
            services.AddHttpClient(provider.HttpClientName, client =>
            {
                client.BaseAddress = new Uri(provider.Options.ApiBaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
                if (provider.Protocol == AiChatProviderProtocol.GEMINI)
                {
                    client.DefaultRequestHeaders.Add("x-goog-api-key", provider.Options.ApiKey);
                    return;
                }

                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", provider.Options.ApiKey);
            });
        }

        services.AddScoped<AiChatService>();
        return services;
    }
}
