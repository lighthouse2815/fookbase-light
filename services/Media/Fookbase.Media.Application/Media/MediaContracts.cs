namespace Fookbase.Media.Application.Media;

public sealed record MediaResponse(
    Guid Id,
    Guid OwnerUserId,
    string FileName,
    string ContentType,
    long Size,
    string Purpose,
    string Url,
    DateTimeOffset CreatedAt);

public sealed record MediaDownload(
    Stream Content,
    string ContentType,
    string FileName);
