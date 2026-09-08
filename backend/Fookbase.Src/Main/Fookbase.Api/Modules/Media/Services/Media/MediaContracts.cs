namespace Fookbase.Api.Modules.Media.Services.Media;

public sealed record CreateUploadRequest(string FileName, string ContentType, long SizeBytes);

public sealed record UploadIntentResponse(
    Guid MediaId,
    string UploadUrl,
    DateTimeOffset ExpiresAtUtc);

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

public sealed record MediaReadUrlResponse(
    Guid MediaId,
    string Url,
    DateTimeOffset ExpiresAtUtc);
