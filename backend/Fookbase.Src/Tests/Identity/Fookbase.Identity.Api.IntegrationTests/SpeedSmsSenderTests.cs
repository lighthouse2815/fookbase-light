using System.Net;
using System.Text;
using Fookbase.Api.Modules.Identity.Config;
using Fookbase.Api.Modules.Identity.Services;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class SpeedSmsSenderTests
{
    [Fact]
    public async Task Sends_otp_to_speed_sms_with_local_phone_and_basic_authentication()
    {
        var handler = new RecordingHandler("{\"status\":\"success\",\"code\":\"00\"}");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.speedsms.vn/") };
        var sender = new SpeedSmsSender(client, EnabledOptions());

        await sender.SendOtpAsync("+84912345678", "123456");

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/index.php/sms/send", handler.Path);
        Assert.Equal("Basic dG9rZW46eA==", handler.Authorization);
        Assert.Contains("\"to\":[\"0912345678\"]", handler.Body, StringComparison.Ordinal);
        Assert.Contains("123456", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"sms_type\":2", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_unsuccessful_speed_sms_response()
    {
        var handler = new RecordingHandler("{\"status\":\"error\",\"code\":\"invalid\"}");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.speedsms.vn/") };
        var sender = new SpeedSmsSender(client, EnabledOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendOtpAsync("+84912345678", "123456"));
    }

    private static SmsOptions EnabledOptions() => new()
    {
        Enabled = true,
        AccessToken = "token",
        Sender = "Fookbase"
    };

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
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
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
