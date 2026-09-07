using Fookbase.Media.Application.Abstractions;
using Fookbase.Media.Application.Media;
using Fookbase.Media.Infrastructure.Persistence;
using Fookbase.Media.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace Fookbase.Media.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMediaInfrastructure(
        this IServiceCollection services,
        string connectionString,
        MinioOptions minioOptions)
    {
        minioOptions.Validate();

        services.AddDbContext<MediaDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton(minioOptions);
        services.AddSingleton<IMinioClient>(_ =>
        {
            var client = new MinioClient()
                .WithEndpoint(minioOptions.Endpoint)
                .WithCredentials(minioOptions.AccessKey, minioOptions.SecretKey);
            if (minioOptions.Secure)
            {
                client = client.WithSSL();
            }

            return client.Build();
        });
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IMediaRepository, MediaRepository>();
        services.AddScoped<IObjectStorage, MinioObjectStorage>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddHostedService<MinioBucketInitializer>();
        return services;
    }
}
