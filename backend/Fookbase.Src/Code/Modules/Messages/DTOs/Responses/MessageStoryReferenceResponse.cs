namespace Fookbase.Api.Modules.Messages.DTOs.Responses;

public sealed record MessageStoryReferenceResponse(
    Guid StoryId,
    bool IsAvailable,
    string? Caption,
    string? MediaType);
