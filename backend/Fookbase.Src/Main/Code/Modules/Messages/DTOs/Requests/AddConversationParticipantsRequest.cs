namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record AddConversationParticipantsRequest(IReadOnlyList<Guid>? UserIds);
