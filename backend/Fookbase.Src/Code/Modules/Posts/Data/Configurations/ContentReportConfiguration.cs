using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Posts.Data.Configurations;

internal sealed class ContentReportConfiguration : IEntityTypeConfiguration<ContentReport>
{
    public void Configure(EntityTypeBuilder<ContentReport> builder)
    {
        builder.ToTable("ContentReports");
        builder.HasKey(report => report.Id);
        builder.Property(report => report.TargetType).HasConversion<int>().IsRequired();
        builder.Property(report => report.Reason).HasConversion<int>().IsRequired();
        builder.Property(report => report.Status).HasConversion<int>().IsRequired();
        builder.Property(report => report.Details).HasMaxLength(ContentReport.MaximumDetailsLength);
        builder.Property(report => report.CreatedAtUtc).IsRequired();
        builder.HasIndex(report => new { report.Status, report.CreatedAtUtc, report.Id });
        builder.HasIndex(report => new { report.ReporterUserId, report.TargetType, report.TargetId }).IsUnique();
        builder.HasIndex(report => new { report.TargetType, report.TargetId, report.Status, report.CreatedAtUtc });
    }
}
