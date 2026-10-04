using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Media.Entities;

[Table("ObjectDeletions")]
[Index(nameof(ProcessedAtUtc), nameof(FailedAtUtc), nameof(NextAttemptAtUtc), nameof(CreatedAtUtc))]
public sealed class ObjectDeletion
{
    public const int MaximumErrorLength = 2000;

    private ObjectDeletion()
    {
    }

    public ObjectDeletion(Guid id, Guid mediaId, string objectKey, DateTimeOffset createdAtUtc)
    {
        Id = id;
        MediaId = mediaId;
        ObjectKey = objectKey;
        CreatedAtUtc = createdAtUtc;
        NextAttemptAtUtc = createdAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }
    public Guid MediaId { get; private set; }

    [MaxLength(MediaAsset.MaximumObjectKeyLength)]
    public string ObjectKey { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public DateTimeOffset? FailedAtUtc { get; private set; }
    public int RetryCount { get; private set; }

    [MaxLength(MaximumErrorLength)]
    public string? LastError { get; private set; }
    [ForeignKey(nameof(MediaId))]
    [InverseProperty(nameof(MediaAsset.ObjectDeletions))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public MediaAsset Media { get; private set; } = null!;

    public void MarkProcessed(DateTimeOffset at)
    {
        ProcessedAtUtc = at;
        LastError = null;
    }

    public void RecordFailure(DateTimeOffset now, DateTimeOffset nextAttemptAtUtc, int retryLimit, string error)
    {
        RetryCount++;
        LastError = error.Length <= MaximumErrorLength ? error : error[..MaximumErrorLength];
        if (RetryCount >= retryLimit)
        {
            FailedAtUtc = now;
            return;
        }

        NextAttemptAtUtc = nextAttemptAtUtc;
    }
}
