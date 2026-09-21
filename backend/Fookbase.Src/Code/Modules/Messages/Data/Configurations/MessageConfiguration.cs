using Fookbase.Api.Modules.Messages.Entities;
using Fookbase.Api.Modules.Stories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Messages.Data.Configurations;

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Type).HasConversion<int>().IsRequired();
        builder.Property(message => message.Content).HasMaxLength(5000);
        builder.Property(message => message.CreatedAtUtc).IsRequired();
        builder.HasIndex(message => new { message.ConversationId, message.CreatedAtUtc, message.Id });
        builder.HasIndex(message => message.ReplyToMessageId);
        builder.HasIndex(message => message.StoryId);
        builder.HasOne<Message>()
            .WithMany()
            .HasForeignKey(message => message.ReplyToMessageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Story>()
            .WithMany()
            .HasForeignKey(message => message.StoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
