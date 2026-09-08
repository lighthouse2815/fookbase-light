using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Posts.Config;

internal sealed class BlockedEdgeConfiguration : IEntityTypeConfiguration<BlockedEdge>
{
    public void Configure(EntityTypeBuilder<BlockedEdge> builder)
    {
        builder.ToTable("BlockedEdges", table => table.HasCheckConstraint(
            "CK_BlockedEdges_DifferentUsers",
            "\"BlockerUserId\" <> \"BlockedUserId\""));
        builder.HasKey(edge => new { edge.BlockerUserId, edge.BlockedUserId });
        builder.Property(edge => edge.CreatedAtUtc).IsRequired();
        builder.Property(edge => edge.LastChangedAtUtc).IsRequired();
        builder.Property(edge => edge.IsActive).IsRequired();
        builder.HasIndex(edge => new { edge.IsActive, edge.BlockedUserId });
    }
}
