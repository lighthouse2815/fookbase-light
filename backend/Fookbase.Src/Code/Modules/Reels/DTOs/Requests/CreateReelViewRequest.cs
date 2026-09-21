namespace Fookbase.Api.Modules.Reels.DTOs.Requests;

public sealed record CreateReelViewRequest(int WatchDurationMs, bool Completed, bool Replayed);
