using Fookbase.Api.Modules.Messages.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Messages.Data.Configurations;

internal sealed class ConversationParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        builder.ToTable("ConversationParticipants");
        builder.HasKey(participant => new { participant.ConversationId, participant.UserId });
        builder.Property(participant => participant.Role).HasConversion<int>().IsRequired();
        builder.Property(participant => participant.Nickname).HasMaxLength(80);
        builder.HasIndex(participant => new { participant.UserId, participant.ConversationId });
        builder.HasIndex(participant => new { participant.ConversationId, participant.UserId });
    }
}
