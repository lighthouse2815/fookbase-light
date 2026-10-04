using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Services;
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
        builder.UseSetting("RateLimiting:SensitiveAuth:LoginPermitLimit", "1000");
        builder.UseSetting("RateLimiting:SensitiveAuth:RecoveryPermitLimit", "1000");
        builder.UseSetting("GoogleAuthentication:Enabled", "true");
        builder.UseSetting("GoogleAuthentication:ClientId", "test-google-client-id");
        builder.UseSetting("GoogleAuthentication:ClientSecret", "test-google-client-secret");
        builder.UseSetting("GoogleAuthentication:WebBaseUrl", "http://web.example.test");
        builder.UseSetting("GoogleAuthentication:ZolaLightBaseUrl", "http://zola.example.test");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<TestEmailSender>();
            services.AddSingleton<IEmailSender>(provider =>
                provider.GetRequiredService<TestEmailSender>());
            services.RemoveAll<IContactOtpSender>();
            services.AddSingleton<TestContactOtpSender>();
            services.AddSingleton<IContactOtpSender>(provider =>
                provider.GetRequiredService<TestContactOtpSender>());
            services.RemoveAll<IGoogleExternalIdentityReader>();
            services.AddScoped<IGoogleExternalIdentityReader, TestGoogleExternalIdentityReader>();
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

public sealed class TestContactOtpSender : IContactOtpSender
{
    private readonly ConcurrentDictionary<string, string> codes = new(StringComparer.Ordinal);

    public Task SendAsync(ContactIdentifier contact, string code, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        codes[contact.Value] = code;
        return Task.CompletedTask;
    }

    public string LastCodeFor(string contact) =>
        codes.TryGetValue(contact, out var code)
            ? code
            : throw new InvalidOperationException("No OTP was sent to this contact.");
}
