namespace Fookbase.Api.Modules.Media.Services;

public sealed record ProcessedVideoMetadata(long DurationMs, int Width, int Height);

public interface IVideoProcessor
{
    Task<ProcessedVideoMetadata> ProcessAsync(
        string inputPath,
        string normalizedVideoPath,
        string posterPath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
