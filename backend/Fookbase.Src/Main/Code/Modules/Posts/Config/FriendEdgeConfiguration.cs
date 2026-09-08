using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Posts.Config;

internal sealed class FriendEdgeConfiguration : IEntityTypeConfiguration<FriendEdge>
{
    public void Configure(EntityTypeBuilder<FriendEdge> builder)
    {
        builder.ToTable("FriendEdges", table => table.HasCheckConstraint(
            "CK_FriendEdges_CanonicalPair",
            "\"UserId1\" < \"UserId2\""));
        builder.HasKey(edge => new { edge.UserId1, edge.UserId2 });
        builder.Property(edge => edge.CreatedAtUtc).IsRequired();
        builder.Property(edge => edge.LastChangedAtUtc).IsRequired();
        builder.Property(edge => edge.IsActive).IsRequired();
        builder.HasIndex(edge => new { edge.IsActive, edge.UserId1 });
        builder.HasIndex(edge => new { edge.IsActive, edge.UserId2 });
    }
}
