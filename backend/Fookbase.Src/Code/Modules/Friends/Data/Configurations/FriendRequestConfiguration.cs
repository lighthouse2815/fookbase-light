using Fookbase.Api.Modules.Friends.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Friends.Data.Configurations;

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
    }
}
