namespace Fookbase.Api.Modules.Groups.DTOs.Responses;

public sealed record GroupJoinRequestResponse(
    Guid Id,
    Guid RequesterUserId,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RespondedAtUtc,
    Guid? RespondedByUserId);
