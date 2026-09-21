namespace Fookbase.Api.Modules.Media.Entities;

public enum MediaProcessingJobStatus
{
    Pending,
    Processing,
    Succeeded,
    Failed
}

public sealed class MediaProcessingJob
{
    private MediaProcessingJob() { }

    private MediaProcessingJob(Guid id, Guid mediaId, DateTimeOffset createdAtUtc)
    {
        Id = id;
        MediaId = mediaId;
        Status = MediaProcessingJobStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        NextAttemptAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid MediaId { get; private set; }
    public MediaProcessingJobStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string? LastError { get; private set; }

    public static MediaProcessingJob Create(Guid mediaId, DateTimeOffset createdAtUtc) =>
        new(Guid.NewGuid(), mediaId, createdAtUtc);

    public void Claim(DateTimeOffset now)
    {
        if (Status is MediaProcessingJobStatus.Succeeded or MediaProcessingJobStatus.Failed)
        {
            throw new InvalidOperationException("A completed processing job cannot be claimed.");
        }

        Status = MediaProcessingJobStatus.Processing;
        AttemptCount++;
        StartedAtUtc = now;
        LastError = null;
    }

    public void Retry(DateTimeOffset nextAttemptAtUtc, string error)
    {
        Status = MediaProcessingJobStatus.Pending;
        NextAttemptAtUtc = nextAttemptAtUtc;
        LastError = NormalizeError(error);
    }

    public void Succeed(DateTimeOffset completedAtUtc)
    {
        Status = MediaProcessingJobStatus.Succeeded;
        CompletedAtUtc = completedAtUtc;
        LastError = null;
    }

    public void Fail(DateTimeOffset completedAtUtc, string error)
    {
        Status = MediaProcessingJobStatus.Failed;
        CompletedAtUtc = completedAtUtc;
        LastError = NormalizeError(error);
    }

    private static string NormalizeError(string error) =>
        error.Length <= 2000 ? error : error[..2000];
}
