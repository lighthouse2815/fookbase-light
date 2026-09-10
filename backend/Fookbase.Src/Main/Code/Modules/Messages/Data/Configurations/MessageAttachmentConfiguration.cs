using Fookbase.Api.Modules.Messages.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Messages.Data.Configurations;

internal sealed class MessageAttachmentConfiguration : IEntityTypeConfiguration<MessageAttachment>
{
    public void Configure(EntityTypeBuilder<MessageAttachment> builder)
    {
        builder.ToTable("MessageAttachments");
        builder.HasKey(attachment => new { attachment.MessageId, attachment.MediaId });
        builder.HasIndex(attachment => attachment.MediaId);
        builder.HasIndex(attachment => new { attachment.MessageId, attachment.SortOrder }).IsUnique();
        builder.HasOne<Message>()
            .WithMany()
            .HasForeignKey(attachment => attachment.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
