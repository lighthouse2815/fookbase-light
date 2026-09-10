namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record UpdateConversationRequest(
    string? Title = null,
    Guid? PhotoMediaId = null,
    bool RemovePhoto = false,
    DateTimeOffset? MutedUntilUtc = null,
    bool? Archived = null,
    string? Nickname = null);
