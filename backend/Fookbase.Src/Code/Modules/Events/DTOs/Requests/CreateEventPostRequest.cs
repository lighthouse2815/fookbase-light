namespace Fookbase.Api.Modules.Events.DTOs.Requests;

public sealed record CreateEventPostRequest(string Content, IReadOnlyList<Guid>? MediaIds);
