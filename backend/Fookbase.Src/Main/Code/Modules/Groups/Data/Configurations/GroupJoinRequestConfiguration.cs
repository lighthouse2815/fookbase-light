using Fookbase.Api.Modules.Groups.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Groups.Data.Configurations;

internal sealed class GroupJoinRequestConfiguration : IEntityTypeConfiguration<GroupJoinRequest>
{
    public void Configure(EntityTypeBuilder<GroupJoinRequest> builder)
    {
        builder.ToTable("GroupJoinRequests");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Status).HasConversion<int>().IsRequired();
        builder.Property(request => request.CreatedAtUtc).IsRequired();
        builder.HasIndex(request => new { request.GroupId, request.RequesterUserId })
            .IsUnique()
            .HasFilter("\"Status\" = 0");
        builder.HasIndex(request => new { request.GroupId, request.Status, request.CreatedAtUtc });
    }
}
