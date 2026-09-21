using Fookbase.Api.Modules.Events.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Fookbase.Api.Modules.Events.Data.Configurations;
internal sealed class EventParticipantConfiguration : IEntityTypeConfiguration<EventParticipant>
{ public void Configure(EntityTypeBuilder<EventParticipant> builder) { builder.ToTable("EventParticipants"); builder.HasKey(x=>new { x.EventId,x.UserId }); builder.Property(x=>x.Status).HasConversion<int>(); builder.HasIndex(x=>new { x.UserId,x.Status }); } }
