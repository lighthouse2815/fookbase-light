using Fookbase.Api.Modules.Messages.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Messages.Data.Configurations;

internal sealed class MessageReactionConfiguration : IEntityTypeConfiguration<MessageReaction>
{
    public void Configure(EntityTypeBuilder<MessageReaction> builder)
    {
        builder.ToTable("MessageReactions");
        builder.HasKey(reaction => new { reaction.MessageId, reaction.UserId });
        builder.Property(reaction => reaction.Type).HasConversion<int>().IsRequired();
        builder.Property(reaction => reaction.CreatedAtUtc).IsRequired();
        builder.HasIndex(reaction => new { reaction.MessageId, reaction.Type });
        builder.HasOne<Message>()
            .WithMany()
            .HasForeignKey(reaction => reaction.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
