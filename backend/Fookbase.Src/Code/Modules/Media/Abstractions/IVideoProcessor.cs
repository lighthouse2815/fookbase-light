using Fookbase.Api.Modules.Media.DTOs.Responses;

namespace Fookbase.Api.Modules.Media.Abstractions;

public interface IVideoProcessor
{
    Task<ProcessedVideoMetadata> ProcessAsync(
        string inputPath,
        string normalizedVideoPath,
        string posterPath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
