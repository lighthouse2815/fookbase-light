namespace Fookbase.Api.Modules.Media.DTOs.Responses;

public sealed record DirectUploadIntent(
    string UploadUrl,
    IReadOnlyDictionary<string, string> UploadParameters);
