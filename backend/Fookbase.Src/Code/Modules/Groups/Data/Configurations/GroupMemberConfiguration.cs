using Fookbase.Api.Modules.Groups.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Groups.Data.Configurations;

internal sealed class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> builder)
    {
        builder.ToTable("GroupMembers");
        builder.HasKey(member => new { member.GroupId, member.UserId });
        builder.Property(member => member.Role).HasConversion<int>().IsRequired();
        builder.Property(member => member.JoinedAtUtc).IsRequired();
        builder.HasIndex(member => new { member.UserId, member.GroupId });
        builder.HasIndex(member => new { member.GroupId, member.Role });
    }
}
