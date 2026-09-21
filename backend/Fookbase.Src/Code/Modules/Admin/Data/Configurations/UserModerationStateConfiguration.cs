using Fookbase.Api.Modules.Admin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fookbase.Api.Modules.Admin.Data.Configurations;

internal sealed class UserModerationStateConfiguration : IEntityTypeConfiguration<UserModerationState>
{
    public void Configure(EntityTypeBuilder<UserModerationState> builder)
    {
        builder.ToTable("UserModerationStates");
        builder.HasKey(state => state.UserId);
    }
}
