using Fookbase.Api.Modules.Media.Entities;

namespace Fookbase.Media.Api.IntegrationTests;

public sealed class MediaJobReliabilityTests
{
    [Fact]
    public void Stale_processing_job_can_be_reclaimed_with_a_new_attempt()
    {
        var now = DateTimeOffset.UtcNow;
        var job = MediaProcessingJob.Create(Guid.NewGuid(), now);

        job.Claim(now);
        job.Claim(now.AddMinutes(3));

        Assert.Equal(MediaProcessingJobStatus.Processing, job.Status);
        Assert.Equal(2, job.AttemptCount);
        Assert.Equal(now.AddMinutes(3), job.StartedAtUtc);
    }

    [Fact]
    public void Object_deletion_stops_retrying_after_its_configured_limit()
    {
        var now = DateTimeOffset.UtcNow;
        var deletion = ObjectDeletion.Create(Guid.NewGuid(), "owner/media/object", now);

        deletion.RecordFailure(now, now.AddMinutes(1), retryLimit: 2, "first failure");
        deletion.RecordFailure(now.AddMinutes(1), now.AddMinutes(2), retryLimit: 2, "second failure");

        Assert.Equal(2, deletion.RetryCount);
        Assert.Equal(now.AddMinutes(1), deletion.FailedAtUtc);
        Assert.Equal("second failure", deletion.LastError);
    }
}
