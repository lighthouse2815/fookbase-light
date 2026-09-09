namespace Fookbase.Api.Modules.Friends.DTOs.Responses;

public sealed record RelationshipStatusResponse(
    Guid UserId,
    string Status,
    Guid? RequestId = null);
