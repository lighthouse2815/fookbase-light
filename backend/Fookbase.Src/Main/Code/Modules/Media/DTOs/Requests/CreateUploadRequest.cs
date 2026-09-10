namespace Fookbase.Api.Modules.Media.DTOs.Requests;

public sealed record CreateUploadRequest(string FileName, string ContentType, long SizeBytes);
