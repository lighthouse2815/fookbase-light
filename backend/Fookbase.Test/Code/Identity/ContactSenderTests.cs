using System.Net;
using System.Text;
using Fookbase.Api.Modules.Identity;
using Fookbase.Api.Modules.Identity.Abstractions;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Config;
using Fookbase.Api.Modules.Identity.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class ContactSenderTests
{
    [Fact]
    public async Task Sends_otp_to_traccar_cloud_service_with_token_authentication()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var services = CreateServices(handler, EnabledOptions());
        var sender = services.GetRequiredService<ContactSender>();

        await sender.SendAsync(ContactIdentifier.Parse("+84912345678"), "123456");

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/sms/", handler.Path);
        Assert.Equal("gateway-token", handler.Authorization);
        Assert.Contains("\"to\":\"\\u002B84912345678\"", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"message\":\"Ma xac nhan Fookbase cua ban la: 123456\"", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_unsuccessful_traccar_response()
    {
        var handler = new RecordingHandler(HttpStatusCode.Unauthorized);
        using var services = CreateServices(handler, EnabledOptions());
        var sender = services.GetRequiredService<ContactSender>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(ContactIdentifier.Parse("+84912345678"), "123456"));
    }

    [Fact]
    public async Task Email_and_otp_contracts_use_the_same_sender_and_sms_works_without_email()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var services = CreateServices(handler, EnabledOptions());
        using var scope = services.CreateScope();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var sender = scope.ServiceProvider.GetRequiredService<IContactOtpSender>();

        Assert.Same(emailSender, sender);
        Assert.False(emailSender.IsEnabled);

        await sender.SendAsync(ContactIdentifier.Parse("0912345678"), "123456");

        Assert.Equal("/sms/", handler.Path);
        Assert.Contains("\"to\":\"\\u002B84912345678\"", handler.Body, StringComparison.Ordinal);
        Assert.Contains("123456", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Disabled_sms_does_not_dispatch_a_request()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var services = CreateServices(handler, new SmsOptions());
        var sender = services.GetRequiredService<ContactSender>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(ContactIdentifier.Parse("0912345678"), "123456"));

        Assert.Null(handler.Method);
    }

    [Fact]
    public async Task Disabled_email_rejects_generic_email_and_email_otp()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var services = CreateServices(handler, EnabledOptions());
        var sender = services.GetRequiredService<ContactSender>();

        var emailFailure = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync("user@example.test", "Subject", "Body"));
        var otpFailure = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(ContactIdentifier.Parse("user@example.test"), "123456"));

        Assert.Equal("SMTP email delivery is not configured.", emailFailure.Message);
        Assert.Equal("Email delivery is not configured.", otpFailure.Message);
        Assert.Null(handler.Method);
    }

    private static ServiceProvider CreateServices(RecordingHandler handler, SmsOptions smsOptions)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityInfrastructure(
            new JwtOptions
            {
                Issuer = "contact-tests",
                Audience = "contact-tests",
                SigningKey = "contact-tests-signing-key-with-more-than-32-characters"
            },
            new EmailOptions(),
            new GoogleAuthenticationOptions(),
            smsOptions);
        services.AddHttpClient(nameof(ContactSender))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    private static SmsOptions EnabledOptions() => new()
    {
        Enabled = true,
        AccessToken = "gateway-token"
    };

    private sealed class RecordingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string? Authorization { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri?.AbsolutePath;
            Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
            };
        }
    }
}
