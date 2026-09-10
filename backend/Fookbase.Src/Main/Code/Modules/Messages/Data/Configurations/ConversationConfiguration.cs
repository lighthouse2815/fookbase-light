using Fookbase.Api.Modules.Messages.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Messages.Data.Configurations;

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");
        builder.HasKey(conversation => conversation.Id);
        builder.Property(conversation => conversation.Type).HasConversion<int>().IsRequired();
        builder.Property(conversation => conversation.Title).HasMaxLength(120);
        builder.Property(conversation => conversation.CreatedAtUtc).IsRequired();
        builder.Property(conversation => conversation.LastMessageAtUtc).IsRequired();
        builder.HasIndex(conversation => new { conversation.UserId1, conversation.UserId2 }).IsUnique();
        builder.HasIndex(conversation => new { conversation.LastMessageAtUtc, conversation.Id });
    }
}
