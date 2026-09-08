using Fookbase.Api.Modules.Friends.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Friends.Data;

internal sealed class FriendRequestConfiguration : IEntityTypeConfiguration<FriendRequest>
{
    public void Configure(EntityTypeBuilder<FriendRequest> builder)
    {
        builder.ToTable("FriendRequests", table =>
        {
            table.HasCheckConstraint(
                "CK_FriendRequests_DifferentUsers",
                "\"SenderUserId\" <> \"ReceiverUserId\"");
            table.HasCheckConstraint(
                "CK_FriendRequests_CanonicalPair",
                "\"UserId1\" < \"UserId2\"");
        });
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Status).HasConversion<int>().IsRequired();
        builder.Property(request => request.CreatedAtUtc).IsRequired();
        builder.HasIndex(request => new { request.UserId1, request.UserId2 })
            .IsUnique()
            .HasFilter("\"Status\" = 0")
            .HasDatabaseName("UX_FriendRequests_PendingPair");
        builder.HasIndex(request => new { request.ReceiverUserId, request.Status, request.CreatedAtUtc });
        builder.HasIndex(request => new { request.SenderUserId, request.Status, request.CreatedAtUtc });
    }
}
