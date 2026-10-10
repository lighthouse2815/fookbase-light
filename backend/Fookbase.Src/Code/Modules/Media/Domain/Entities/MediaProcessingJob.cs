using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fookbase.Api.Modules.Media.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Media.Entities;

[Index(nameof(MediaId), IsUnique = true)]
[Index(nameof(Status), nameof(NextAttemptAtUtc), nameof(CreatedAtUtc))]
public sealed class MediaProcessingJob
{
    public const int MaximumErrorLength = 2000;

    private MediaProcessingJob()
    {
    }

    public MediaProcessingJob(Guid id, Guid mediaId, DateTimeOffset createdAtUtc)
    {
        Id = id;
        MediaId = mediaId;
        Status = MediaProcessingJobStatus.PENDING;
        CreatedAtUtc = createdAtUtc;
        NextAttemptAtUtc = createdAtUtc;
    }

    [Key]
    public Guid Id { get; private set; }
    public Guid MediaId { get; private set; }
    public MediaProcessingJobStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    [MaxLength(MaximumErrorLength)]
    public string? LastError { get; private set; }

    [ForeignKey(nameof(MediaId))]
    [InverseProperty(nameof(MediaAsset.ProcessingJob))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public MediaAsset Media { get; private set; } = null!;

    public void Claim(DateTimeOffset now)
    {
        if (Status is MediaProcessingJobStatus.SUCCEEDED or MediaProcessingJobStatus.FAILED)
        {
            throw new InvalidOperationException("A completed processing job cannot be claimed.");
        }

        Status = MediaProcessingJobStatus.PROCESSING;
        AttemptCount++;
        StartedAtUtc = now;
        LastError = null;
    }

    public void Retry(DateTimeOffset nextAttemptAtUtc, string error)
    {
        Status = MediaProcessingJobStatus.PENDING;
        NextAttemptAtUtc = nextAttemptAtUtc;
        LastError = NormalizeError(error);
    }

    public void Succeed(DateTimeOffset completedAtUtc)
    {
        Status = MediaProcessingJobStatus.SUCCEEDED;
        CompletedAtUtc = completedAtUtc;
        LastError = null;
    }

    public void Fail(DateTimeOffset completedAtUtc, string error)
    {
        Status = MediaProcessingJobStatus.FAILED;
        CompletedAtUtc = completedAtUtc;
        LastError = NormalizeError(error);
    }

    private static string NormalizeError(string error) =>
        error.Length <= MaximumErrorLength ? error : error[..MaximumErrorLength];
}
