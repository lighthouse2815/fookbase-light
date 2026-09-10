using Fookbase.Api.Modules.Friends.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Friends.Data.Configurations;

internal sealed class FriendNotificationConfiguration : IEntityTypeConfiguration<FriendNotification>
{
    public void Configure(EntityTypeBuilder<FriendNotification> builder)
    {
        builder.ToTable("FriendNotifications");
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Type).IsRequired();
        builder.Property(notification => notification.CreatedAtUtc).IsRequired();
        builder.HasIndex(notification => new
        {
            notification.RecipientUserId,
            notification.ReadAtUtc,
            notification.CreatedAtUtc
        });
    }
}
