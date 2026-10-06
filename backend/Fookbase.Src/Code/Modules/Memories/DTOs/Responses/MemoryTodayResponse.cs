namespace Fookbase.Api.Modules.Memories.DTOs.Responses;

public sealed record MemoryTodayResponse(string Date, IReadOnlyList<MemoryYearResponse> Years);
