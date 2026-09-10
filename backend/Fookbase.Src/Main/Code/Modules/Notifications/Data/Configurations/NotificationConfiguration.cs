using Fookbase.Api.Modules.Notifications.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Notifications.Data.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Type).HasConversion<int>().IsRequired();
        builder.Property(notification => notification.EntityType).HasConversion<int>();
        builder.Property(notification => notification.IsRead).IsRequired();
        builder.Property(notification => notification.CreatedAtUtc).IsRequired();
        builder.HasIndex(notification => new
        {
            notification.RecipientUserId,
            notification.CreatedAtUtc,
            notification.Id
        });
        builder.HasIndex(notification => new
        {
            notification.RecipientUserId,
            notification.IsRead,
            notification.CreatedAtUtc
        });
        builder.HasIndex(notification => new
        {
            notification.RecipientUserId,
            notification.ActorUserId,
            notification.Type,
            notification.EntityType,
            notification.EntityId
        });
    }
}
