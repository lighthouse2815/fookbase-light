using Fookbase.Api.Modules.Media.Domain.Enums;

namespace Fookbase.Api.Modules.Media.DTOs.Responses;

public sealed record StoredObjectInfo(
    long SizeBytes,
    MediaType MediaType,
    bool IsAuthenticated);
