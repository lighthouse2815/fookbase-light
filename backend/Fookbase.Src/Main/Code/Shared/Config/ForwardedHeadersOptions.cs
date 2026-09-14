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
            throw new InvalidOperationException("ForwardedHeaders:ForwardLimit must be between 1 and 2.");
        }

        if (KnownProxies.Length == 0)
        {
            throw new InvalidOperationException(
                "ForwardedHeaders:KnownProxies is required when forwarded headers are enabled.");
        }

        if (KnownProxies.Any(proxy => !IPAddress.TryParse(proxy, out _)))
        {
            throw new InvalidOperationException("ForwardedHeaders:KnownProxies must contain valid IP addresses.");
        }
    }
}
