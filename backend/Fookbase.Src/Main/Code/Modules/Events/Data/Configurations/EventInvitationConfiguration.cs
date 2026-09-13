using Fookbase.Api.Modules.Events.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Fookbase.Api.Modules.Events.Data.Configurations;
internal sealed class EventInvitationConfiguration : IEntityTypeConfiguration<EventInvitation>
{ public void Configure(EntityTypeBuilder<EventInvitation> builder) { builder.ToTable("EventInvitations"); builder.HasKey(x=>x.Id); builder.Property(x=>x.Status).HasConversion<int>(); builder.HasIndex(x=>new { x.EventId,x.InviteeUserId }).HasFilter("\"Status\" = 0"); builder.HasIndex(x=>new { x.InviteeUserId,x.Status,x.CreatedAtUtc }); } }
