using System.Diagnostics;
using System.Text.Json;

namespace Fookbase.Api.Modules.Media.Services;

internal sealed class FfmpegVideoProcessor : IVideoProcessor
{
    public async Task<ProcessedVideoMetadata> ProcessAsync(
        string inputPath,
        string normalizedVideoPath,
        string posterPath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var probe = await ProbeAsync(inputPath, timeout, cancellationToken);
        if (probe.DurationMs <= 0 || probe.Width <= 0 || probe.Height <= 0)
        {
            throw new InvalidOperationException("The uploaded video has no valid video stream.");
        }

        await RunAsync("ffmpeg",
        [
            "-y", "-hide_banner", "-loglevel", "error", "-i", inputPath,
            "-map", "0:v:0", "-map", "0:a?",
            "-c:v", "libx264", "-preset", "veryfast", "-crf", "23",
            "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "128k",
            "-movflags", "+faststart",
            "-vf", "scale=w='min(1280,iw)':h=-2:force_original_aspect_ratio=decrease",
            normalizedVideoPath
        ], timeout, cancellationToken);

        var normalized = await ProbeAsync(normalizedVideoPath, timeout, cancellationToken);
        if (normalized.DurationMs <= 0 || normalized.Width <= 0 || normalized.Height <= 0)
        {
            throw new InvalidOperationException("FFmpeg did not produce a valid normalized video.");
        }

        await RunAsync("ffmpeg",
        [
            "-y", "-hide_banner", "-loglevel", "error", "-ss", "0", "-i", normalizedVideoPath,
            "-frames:v", "1", "-vf", "scale=w='min(720,iw)':h=-2", posterPath
        ], timeout, cancellationToken);

        return normalized;
    }

    private static async Task<ProcessedVideoMetadata> ProbeAsync(
        string path,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var output = await RunAsync("ffprobe",
        [
            "-v", "error", "-select_streams", "v:0",
            "-show_entries", "format=duration,format_name:stream=codec_name,width,height",
            "-of", "json", path
        ], timeout, cancellationToken);

        try
        {
            using var document = JsonDocument.Parse(output);
            var format = document.RootElement.GetProperty("format");
            var durationText = format.GetProperty("duration").GetString();
            var stream = document.RootElement.GetProperty("streams").EnumerateArray().FirstOrDefault();
            if (!double.TryParse(durationText, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var durationSeconds) ||
                durationSeconds <= 0 || stream.ValueKind == JsonValueKind.Undefined)
            {
                throw new InvalidOperationException("The uploaded video is not valid media.");
            }

            return new ProcessedVideoMetadata(
                checked((long)Math.Ceiling(durationSeconds * 1000)),
                stream.GetProperty("width").GetInt32(),
                stream.GetProperty("height").GetInt32());
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("ffprobe did not return usable video metadata.", exception);
        }
    }

    private static async Task<string> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Unable to start {executable}.");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutSource.Token);
        try
        {
            await process.WaitForExitAsync(linkedSource.Token);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested &&
                                                 !cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw new TimeoutException($"{executable} exceeded the configured processing timeout.");
        }

        var output = await stdout;
        var error = await stderr;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{executable} failed with exit code {process.ExitCode}: {TrimError(error)}");
        }

        return output;
    }

    private static string TrimError(string value) => value.Length <= 500 ? value : value[..500];
}
