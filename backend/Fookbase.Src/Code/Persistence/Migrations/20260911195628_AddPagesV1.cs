using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPagesV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateTable(
                name: "PageFollowers",
                columns: table => new
                {
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FollowedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageFollowers", x => new { x.PageId, x.UserId });
                });

            migrationBuilder.CreateTable(
                name: "PageMediaReferences",
                columns: table => new
                {
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttachedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageMediaReferences", x => new { x.PageId, x.Slot });
                });

            migrationBuilder.CreateTable(
                name: "PageMembers",
                columns: table => new
                {
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    JoinedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageMembers", x => new { x.PageId, x.UserId });
                });

            migrationBuilder.CreateTable(
                name: "PageRoleInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviteeUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespondedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageRoleInvitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Username = table.Column<string>(type: "citext", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Bio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AvatarMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CoverMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PageFollowers_UserId_FollowedAtUtc",
                table: "PageFollowers",
                columns: new[] { "UserId", "FollowedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PageMediaReferences_MediaId",
                table: "PageMediaReferences",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_PageMembers_PageId_Role",
                table: "PageMembers",
                columns: new[] { "PageId", "Role" });

            migrationBuilder.CreateIndex(
                name: "IX_PageMembers_UserId_PageId",
                table: "PageMembers",
                columns: new[] { "UserId", "PageId" });

            migrationBuilder.CreateIndex(
                name: "IX_PageRoleInvitations_InviteeUserId_Status_CreatedAtUtc",
                table: "PageRoleInvitations",
                columns: new[] { "InviteeUserId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PageRoleInvitations_PageId_InviteeUserId",
                table: "PageRoleInvitations",
                columns: new[] { "PageId", "InviteeUserId" },
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Pages_CreatedByUserId_CreatedAtUtc",
                table: "Pages",
                columns: new[] { "CreatedByUserId", "CreatedAtUtc" },
                filter: "\"DeletedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Pages_Status_Name_Id",
                table: "Pages",
                columns: new[] { "Status", "Name", "Id" },
                filter: "\"DeletedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Pages_Username",
                table: "Pages",
                column: "Username",
                unique: true,
                filter: "\"DeletedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PageFollowers");

            migrationBuilder.DropTable(
                name: "PageMediaReferences");

            migrationBuilder.DropTable(
                name: "PageMembers");

            migrationBuilder.DropTable(
                name: "PageRoleInvitations");

            migrationBuilder.DropTable(
                name: "Pages");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,");
        }
    }
}
