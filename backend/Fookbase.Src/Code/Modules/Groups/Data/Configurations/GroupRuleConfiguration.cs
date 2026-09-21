using Fookbase.Api.Modules.Groups.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Groups.Data.Configurations;

internal sealed class GroupRuleConfiguration : IEntityTypeConfiguration<GroupRule>
{
    public void Configure(EntityTypeBuilder<GroupRule> builder)
    {
        builder.ToTable("GroupRules");
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Title).HasMaxLength(GroupRule.MaximumTitleLength).IsRequired();
        builder.Property(rule => rule.Description).HasMaxLength(GroupRule.MaximumDescriptionLength);
        builder.HasIndex(rule => new { rule.GroupId, rule.SortOrder, rule.Id });
    }
}
