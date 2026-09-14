namespace Fookbase.Api.Modules.Media.DTOs.Responses;

public sealed record UploadIntentResponse(
    Guid MediaId,
    string UploadUrl,
    string UploadMethod,
    IReadOnlyDictionary<string, string> UploadParameters,
    DateTimeOffset ExpiresAtUtc);
