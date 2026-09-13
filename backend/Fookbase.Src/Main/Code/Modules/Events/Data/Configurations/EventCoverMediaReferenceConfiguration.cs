using Fookbase.Api.Modules.Events.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Fookbase.Api.Modules.Events.Data.Configurations;
internal sealed class EventCoverMediaReferenceConfiguration : IEntityTypeConfiguration<EventCoverMediaReference>
{ public void Configure(EntityTypeBuilder<EventCoverMediaReference> builder) { builder.ToTable("EventCoverMediaReferences"); builder.HasKey(x=>x.EventId); builder.HasIndex(x=>x.MediaId); } }
