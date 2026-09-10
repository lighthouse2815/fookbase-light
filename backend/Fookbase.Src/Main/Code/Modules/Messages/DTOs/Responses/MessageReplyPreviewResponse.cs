namespace Fookbase.Api.Modules.Messages.DTOs.Responses;

public sealed record MessageReplyPreviewResponse(
    Guid Id,
    Guid SenderUserId,
    string? Content,
    string Type,
    bool IsDeleted);
