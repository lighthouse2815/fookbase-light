using System.Net;
using System.Text;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Config;
using Fookbase.Api.Modules.Identity.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class TraccarSmsSenderTests
{
    [Fact]
    public async Task Sends_otp_to_traccar_cloud_service_with_token_authentication()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.traccar.org/sms/") };
        var sender = new TraccarSmsSender(client, EnabledOptions(), NullLogger<TraccarSmsSender>.Instance);

        var code = await sender.SendOtpAsync("+84912345678", "123456");

        Assert.Equal("123456", code);
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
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.traccar.org/sms/") };
        var sender = new TraccarSmsSender(client, EnabledOptions(), NullLogger<TraccarSmsSender>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendOtpAsync("+84912345678", "123456"));
    }

    [Fact]
    public async Task Contact_sender_delivers_phone_otp_through_traccar()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.traccar.org/sms/") };
        var smsSender = new TraccarSmsSender(client, EnabledOptions(), NullLogger<TraccarSmsSender>.Instance);
        var emailSender = new TestEmailSender();
        var sender = new ContactOtpSender(emailSender, smsSender);

        var code = await sender.SendAsync(ContactIdentifier.Parse("0912345678"), "123456");

        Assert.Equal("123456", code);
        Assert.Equal("/sms/", handler.Path);
        Assert.Contains("\"to\":\"\\u002B84912345678\"", handler.Body, StringComparison.Ordinal);
        Assert.Contains("123456", handler.Body, StringComparison.Ordinal);
        Assert.Empty(emailSender.Emails);
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
