namespace Fookbase.Api.Modules.Events.DTOs.Requests;

public sealed record CreateEventRequest(string HostType, Guid? HostId, string Name, string? Description, string Privacy,
    string LocationType, string? LocationName, string? Address, string? OnlineUrl, DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc, Guid? CoverMediaId, string? Status = null);
