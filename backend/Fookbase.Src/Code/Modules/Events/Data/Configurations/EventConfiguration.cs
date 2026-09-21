using Fookbase.Api.Modules.Events.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Fookbase.Api.Modules.Events.Data.Configurations;
internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{ public void Configure(EntityTypeBuilder<Event> builder) { builder.ToTable("Events"); builder.HasKey(x=>x.Id); builder.Property(x=>x.Name).HasMaxLength(Event.MaximumNameLength).IsRequired(); builder.Property(x=>x.Description).HasMaxLength(Event.MaximumDescriptionLength); builder.Property(x=>x.HostType).HasConversion<int>(); builder.Property(x=>x.Privacy).HasConversion<int>(); builder.Property(x=>x.LocationType).HasConversion<int>(); builder.Property(x=>x.Status).HasConversion<int>(); builder.Property(x=>x.CreatedAtUtc).IsRequired(); builder.HasIndex(x=>new { x.Status,x.Privacy,x.StartsAtUtc }).HasFilter("\"DeletedAtUtc\" IS NULL"); builder.HasIndex(x=>new { x.HostType,x.HostId,x.StartsAtUtc }).HasFilter("\"DeletedAtUtc\" IS NULL"); } }
