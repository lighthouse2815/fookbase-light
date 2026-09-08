namespace Fookbase.Media.Infrastructure.IntegrationEvents;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";
    public bool PublisherEnabled { get; init; } = true;
    public int PollingIntervalSeconds { get; init; } = 2;
    public int FailureBackoffSeconds { get; init; } = 5;
    public int BatchSize { get; init; } = 20;
    public void Validate()
    {
        if (PollingIntervalSeconds <= 0 || FailureBackoffSeconds <= 0 || BatchSize <= 0)
            throw new InvalidOperationException("Outbox settings must be positive.");
    }
}
