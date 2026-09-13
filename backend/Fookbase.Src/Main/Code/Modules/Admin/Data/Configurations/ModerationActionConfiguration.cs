using Fookbase.Api.Modules.Admin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Admin.Data.Configurations;

internal sealed class ModerationActionConfiguration : IEntityTypeConfiguration<ModerationAction>
{
    public void Configure(EntityTypeBuilder<ModerationAction> builder)
    {
        builder.ToTable("ModerationActions");
        builder.HasKey(action => action.Id);
        builder.Property(action => action.TargetType).HasConversion<int>().IsRequired();
        builder.Property(action => action.ActionType).HasConversion<int>().IsRequired();
        builder.Property(action => action.Reason).HasMaxLength(ModerationAction.MaximumReasonLength).IsRequired();
        builder.Property(action => action.InternalNote).HasMaxLength(ModerationAction.MaximumInternalNoteLength);
        builder.HasIndex(action => new { action.TargetType, action.TargetId, action.CreatedAtUtc });
        builder.HasIndex(action => new { action.SubjectUserId, action.CreatedAtUtc });
    }
}
