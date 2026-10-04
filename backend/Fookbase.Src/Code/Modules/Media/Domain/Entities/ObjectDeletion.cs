namespace Fookbase.Api.Modules.Media.Entities;

public sealed class ObjectDeletion
{
    private ObjectDeletion() { }
    public ObjectDeletion(Guid id, Guid mediaId, string objectKey, DateTimeOffset createdAtUtc)
    {
        Id = id; MediaId = mediaId; ObjectKey = objectKey; CreatedAtUtc = createdAtUtc;
        NextAttemptAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid MediaId { get; private set; }
    public string ObjectKey { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public DateTimeOffset? FailedAtUtc { get; private set; }
    public int RetryCount { get; private set; }
    public string? LastError { get; private set; }
    public void MarkProcessed(DateTimeOffset at)
    {
        ProcessedAtUtc = at;
        LastError = null;
    }

    public void RecordFailure(DateTimeOffset now, DateTimeOffset nextAttemptAtUtc, int retryLimit, string error)
    {
        RetryCount++;
        LastError = error.Length <= 2000 ? error : error[..2000];
        if (RetryCount >= retryLimit)
        {
            FailedAtUtc = now;
            return;
        }

        NextAttemptAtUtc = nextAttemptAtUtc;
    }
}
