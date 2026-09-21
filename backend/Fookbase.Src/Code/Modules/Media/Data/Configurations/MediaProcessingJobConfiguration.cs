using Fookbase.Api.Modules.Media.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Media.Data.Configurations;

internal sealed class MediaProcessingJobConfiguration : IEntityTypeConfiguration<MediaProcessingJob>
{
    public void Configure(EntityTypeBuilder<MediaProcessingJob> builder)
    {
        builder.ToTable("MediaProcessingJobs");
        builder.HasKey(job => job.Id);
        builder.Property(job => job.Status).HasConversion<int>().IsRequired();
        builder.Property(job => job.CreatedAtUtc).IsRequired();
        builder.Property(job => job.NextAttemptAtUtc).IsRequired();
        builder.Property(job => job.LastError).HasMaxLength(2000);
        builder.HasIndex(job => job.MediaId).IsUnique();
        builder.HasIndex(job => new { job.Status, job.NextAttemptAtUtc, job.CreatedAtUtc });
    }
}
