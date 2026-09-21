using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Code.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventsV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventCoverMediaReferences",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttachedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventCoverMediaReferences", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "EventInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviteeUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespondedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventInvitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventParticipants",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RespondedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventParticipants", x => new { x.EventId, x.UserId });
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    HostType = table.Column<int>(type: "integer", nullable: false),
                    HostId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Privacy = table.Column<int>(type: "integer", nullable: false),
                    LocationType = table.Column<int>(type: "integer", nullable: false),
                    LocationName = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    OnlineUrl = table.Column<string>(type: "text", nullable: true),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CoverMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventCoverMediaReferences_MediaId",
                table: "EventCoverMediaReferences",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_EventInvitations_EventId_InviteeUserId",
                table: "EventInvitations",
                columns: new[] { "EventId", "InviteeUserId" },
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EventInvitations_InviteeUserId_Status_CreatedAtUtc",
                table: "EventInvitations",
                columns: new[] { "InviteeUserId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EventParticipants_UserId_Status",
                table: "EventParticipants",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_HostType_HostId_StartsAtUtc",
                table: "Events",
                columns: new[] { "HostType", "HostId", "StartsAtUtc" },
                filter: "\"DeletedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Status_Privacy_StartsAtUtc",
                table: "Events",
                columns: new[] { "Status", "Privacy", "StartsAtUtc" },
                filter: "\"DeletedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventCoverMediaReferences");

            migrationBuilder.DropTable(
                name: "EventInvitations");

            migrationBuilder.DropTable(
                name: "EventParticipants");

            migrationBuilder.DropTable(
                name: "Events");
        }
    }
}
