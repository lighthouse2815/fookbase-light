namespace Fookbase.Media.Infrastructure.IntegrationEvents;

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
        if (string.IsNullOrWhiteSpace(HostName) || Port <= 0 ||
            string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password) ||
            FailureBackoffSeconds <= 0)
            throw new InvalidOperationException("Valid RabbitMQ connection and backoff settings are required.");
    }
}
