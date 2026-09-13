using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Fookbase.Api.Modules.Identity.Data.Configurations;
internal sealed class TwoFactorLoginChallengeConfiguration : IEntityTypeConfiguration<TwoFactorLoginChallenge>
{ public void Configure(EntityTypeBuilder<TwoFactorLoginChallenge> builder) { builder.ToTable("TwoFactorLoginChallenges"); builder.HasKey(item => item.Id); builder.HasIndex(item => new { item.UserId, item.ExpiresAtUtc }); } }
