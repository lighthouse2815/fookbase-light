using Fookbase.Api.Modules.Posts.DTOs.Responses;

namespace Fookbase.Api.Modules.Memories.DTOs.Responses;

public sealed record MemoryTodayResponse(string Date, IReadOnlyList<MemoryYearResponse> Years);

public sealed record MemoryYearResponse(int Year, int YearsAgo, IReadOnlyList<PostResponse> Items);
