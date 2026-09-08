namespace Fookbase.Api.Modules.Media.Services.Abstractions;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(IntegrationEventMessage message, CancellationToken cancellationToken = default);
}

public sealed record IntegrationEventMessage(
    Guid Id,
    string Type,
    string Payload,
    DateTimeOffset OccurredAtUtc);
