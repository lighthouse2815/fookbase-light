using Fookbase.Api.Modules.Messages.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Messages.Data.Configurations;

internal sealed class MessageNotificationConfiguration : IEntityTypeConfiguration<MessageNotification>
{
    public void Configure(EntityTypeBuilder<MessageNotification> builder)
    {
        builder.ToTable("MessageNotifications");
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.CreatedAtUtc).IsRequired();
        builder.HasIndex(notification => new
        {
            notification.RecipientUserId,
            notification.MessageId
        }).IsUnique();
        builder.HasIndex(notification => new
        {
            notification.RecipientUserId,
            notification.ReadAtUtc,
            notification.CreatedAtUtc
        });
    }
}
