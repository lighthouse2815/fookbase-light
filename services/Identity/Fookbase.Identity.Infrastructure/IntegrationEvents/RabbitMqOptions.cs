namespace Fookbase.Identity.Infrastructure.IntegrationEvents;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; init; } = "localhost";

    public int Port { get; init; } = 5672;

    public string UserName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string VirtualHost { get; init; } = "/";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HostName) || Port <= 0)
        {
            throw new InvalidOperationException("RabbitMQ host and port are required.");
        }

        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password))
        {
            throw new InvalidOperationException("RabbitMQ credentials are required.");
        }
    }
}
