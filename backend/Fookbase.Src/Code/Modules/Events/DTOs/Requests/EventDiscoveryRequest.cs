using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Events.Services;

namespace Fookbase.Api.Modules.Events.DTOs.Requests;

public sealed record EventDiscoveryRequest(
    string? Query = null,
    string? Cursor = null,

    [Range(1, EventsService.MaximumPageSize, ErrorMessage = "Số kết quả phải từ {1} đến {2}.")]
    int Limit = EventsService.DefaultPageSize);
