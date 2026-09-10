using Fookbase.Api.Modules.Messages.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Messages.Data.Configurations;

internal sealed class ConversationReadCursorConfiguration : IEntityTypeConfiguration<ConversationReadCursor>
{
    public void Configure(EntityTypeBuilder<ConversationReadCursor> builder)
    {
        builder.ToTable("ConversationReadCursors");
        builder.HasKey(cursor => new { cursor.ConversationId, cursor.UserId });
        builder.HasIndex(cursor => new { cursor.UserId, cursor.ConversationId });
    }
}
