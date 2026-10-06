using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Reels.DTOs.Requests;

public sealed record CreateReelViewRequest(
    [Range(1, int.MaxValue, ErrorMessage = "Thời gian xem phải lớn hơn 0.")]
    int WatchDurationMs,
    bool Completed,
    bool Replayed);
