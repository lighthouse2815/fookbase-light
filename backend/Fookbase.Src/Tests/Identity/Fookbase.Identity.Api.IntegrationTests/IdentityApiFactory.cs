using Fookbase.Api.Modules.Identity.Data;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Users.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class IdentityApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__IdentityDatabase")
            ?? throw new InvalidOperationException("Identity development database connection string is required.");
        builder.UseEnvironment("Testing");
        ConfigureModuleConnections(builder, connectionString);
        builder.UseSetting("Minio:AccessKey", "integration-tests");
        builder.UseSetting("Minio:SecretKey", "integration-tests");
        builder.UseSetting("Minio:BucketInitializationEnabled", "false");
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
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        dbContext.Database.Migrate();
        scope.ServiceProvider.GetRequiredService<UsersDbContext>().Database.Migrate();

        return host;
    }

    private static void ConfigureModuleConnections(IWebHostBuilder builder, string connectionString)
    {
        foreach (var module in new[] { "Identity", "Users", "Friends", "Messages", "Posts", "Media" })
        {
            builder.UseSetting($"ConnectionStrings:{module}Database", connectionString);
        }
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
