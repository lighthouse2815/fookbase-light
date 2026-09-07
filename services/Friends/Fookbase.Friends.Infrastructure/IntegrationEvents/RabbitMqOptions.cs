namespace Fookbase.Friends.Infrastructure.IntegrationEvents;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; init; } = "localhost";

    public int Port { get; init; } = 5672;

    public string UserName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string VirtualHost { get; init; } = "/";

    public bool ConsumerEnabled { get; init; } = true;

    public int FailureBackoffSeconds { get; init; } = 5;

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

        if (FailureBackoffSeconds <= 0)
        {
            throw new InvalidOperationException("RabbitMQ failure backoff must be positive.");
        }
    }
}
