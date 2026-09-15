using Fookbase.Api.Modules.Identity.Data;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Users.Data;
using Fookbase.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace Fookbase.Identity.Api.IntegrationTests;

public class IdentityApiFactory : WebApplicationFactory<Program>
{
    private readonly string? connectionStringOverride;

    public IdentityApiFactory()
    {
    }

    protected IdentityApiFactory(string connectionStringOverride)
    {
        this.connectionStringOverride = connectionStringOverride;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = connectionStringOverride
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__FookbaseDatabase")
            ?? throw new InvalidOperationException("Fookbase development database connection string is required.");
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:FookbaseDatabase", connectionString);
        builder.UseSetting("Minio:AccessKey", "integration-tests");
        builder.UseSetting("Minio:SecretKey", "integration-tests");
        builder.UseSetting("Minio:BucketInitializationEnabled", "false");
        builder.UseSetting("Cloudinary:CloudName", "integration-tests");
        builder.UseSetting("Cloudinary:ApiKey", "test-api-key");
        builder.UseSetting("Cloudinary:ApiSecret", "test-api-secret");
        builder.UseSetting("Media:CleanupIntervalSeconds", "3600");
        builder.UseSetting("Jwt:SigningKey", "identity-integration-tests-signing-key-with-32-characters");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<TestEmailSender>();
            services.AddSingleton<IEmailSender>(provider =>
                provider.GetRequiredService<TestEmailSender>());
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        dbContext.Database.Migrate();
        return host;
    }
}

public sealed class TestEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<SentEmail> emails = new();

    public bool IsEnabled => true;

    public IReadOnlyCollection<SentEmail> Emails => emails.ToArray();

    public Task SendAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        emails.Enqueue(new SentEmail(recipientEmail, subject, htmlBody));
        return Task.CompletedTask;
    }
}

public sealed record SentEmail(string RecipientEmail, string Subject, string HtmlBody);
