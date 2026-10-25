using System.Net;

namespace Fookbase.Api.Shared.Config;

public sealed class ForwardedHeadersOptions
{
    public const string SectionName = "ForwardedHeaders";

    public bool Enabled { get; init; }

    public string[] KnownProxies { get; init; } = [];

    public int ForwardLimit { get; init; } = 1;

    public void Validate(bool production)
    {
        if (!Enabled)
        {
            return;
        }

        if (ForwardLimit is < 1 or > 2)
        {
            throw new InvalidOperationException("ForwardedHeaders:ForwardLimit phải nằm trong khoảng từ 1 đến 2.");
        }

        if (KnownProxies.Length == 0)
        {
            throw new InvalidOperationException(
                "Phải cấu hình ForwardedHeaders:KnownProxies khi bật xử lý header chuyển tiếp.");
        }

        if (KnownProxies.Any(proxy => !IPAddress.TryParse(proxy, out _)))
        {
            throw new InvalidOperationException("ForwardedHeaders:KnownProxies phải chứa các địa chỉ IP hợp lệ.");
        }
    }
}
