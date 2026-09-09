namespace Fookbase.Api.Modules.Media.DTOs.Responses;

public sealed record MediaResponse(
    Guid Id,
    Guid OwnerUserId,
    string MediaType,
    string Status,
    string FileName,
    string ContentType,
    long DeclaredSizeBytes,
    long? ActualSizeBytes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UploadExpiresAtUtc,
    DateTimeOffset? UploadedAtUtc,
    DateTimeOffset? DeletedAtUtc);
