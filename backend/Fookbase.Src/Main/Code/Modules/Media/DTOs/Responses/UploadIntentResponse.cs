namespace Fookbase.Api.Modules.Media.DTOs.Responses;

public sealed record UploadIntentResponse(
    Guid MediaId,
    string UploadUrl,
    DateTimeOffset ExpiresAtUtc);
