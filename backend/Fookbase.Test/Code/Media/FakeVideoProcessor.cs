using Fookbase.Api.Modules.Media.Services;

namespace Fookbase.Media.Api.IntegrationTests;

public sealed class FakeVideoProcessor : IVideoProcessor
{
    public async Task<ProcessedVideoMetadata> ProcessAsync(
        string inputPath,
        string normalizedVideoPath,
        string posterPath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var input = await File.ReadAllBytesAsync(inputPath, cancellationToken);
        if (input.Contains((byte)0xff))
        {
            throw new InvalidOperationException("The fake processor rejected corrupt input.");
        }

        await File.WriteAllBytesAsync(normalizedVideoPath, input, cancellationToken);
        await File.WriteAllBytesAsync(posterPath,
            [0xff, 0xd8, 0xff, 0xe0, 0x00, 0x10, 0x4a, 0x46, 0x49, 0x46], cancellationToken);
        return new ProcessedVideoMetadata(10_000, 720, 1280);
    }
}
