using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Modules.Media.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class CorrectMediaLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Size",
                table: "MediaAssets",
                newName: "DeclaredSizeBytes");

            migrationBuilder.RenameColumn(
                name: "Purpose",
                table: "MediaAssets",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "ObjectName",
                table: "MediaAssets",
                newName: "ObjectKey");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                table: "MediaAssets",
                newName: "DeletedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "MediaAssets",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_MediaAssets_OwnerUserId_CreatedAt",
                table: "MediaAssets",
                newName: "IX_MediaAssets_OwnerUserId_CreatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_MediaAssets_ObjectName",
                table: "MediaAssets",
                newName: "IX_MediaAssets_ObjectKey");

            migrationBuilder.AddColumn<long>(
                name: "ActualSizeBytes",
                table: "MediaAssets",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UploadedAtUtc",
                table: "MediaAssets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediaType",
                table: "MediaAssets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UploadExpiresAtUtc",
                table: "MediaAssets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"MediaAssets\" SET \"Status\" = CASE WHEN \"DeletedAtUtc\" IS NULL THEN 2 ELSE 3 END");

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "KnownUsers",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RegisteredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownUsers", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "MediaReferences",
                columns: table => new
                {
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttachedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaReferences", x => new { x.MediaId, x.PostId });
                });

            migrationBuilder.CreateTable(
                name: "ObjectDeletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObjectDeletions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_Status_UploadExpiresAtUtc",
                table: "MediaAssets",
                columns: new[] { "Status", "UploadExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaReferences_PostId",
                table: "MediaReferences",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_ObjectDeletions_ProcessedAtUtc_CreatedAtUtc",
                table: "ObjectDeletions",
                columns: new[] { "ProcessedAtUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAtUtc", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxMessages");

            migrationBuilder.DropTable(
                name: "KnownUsers");

            migrationBuilder.DropTable(
                name: "MediaReferences");

            migrationBuilder.DropTable(
                name: "ObjectDeletions");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_MediaAssets_Status_UploadExpiresAtUtc",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "ActualSizeBytes",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "UploadedAtUtc",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "UploadExpiresAtUtc",
                table: "MediaAssets");

            migrationBuilder.RenameColumn(
                name: "DeletedAtUtc",
                table: "MediaAssets",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "MediaAssets",
                newName: "Purpose");

            migrationBuilder.RenameColumn(
                name: "ObjectKey",
                table: "MediaAssets",
                newName: "ObjectName");

            migrationBuilder.RenameColumn(
                name: "DeclaredSizeBytes",
                table: "MediaAssets",
                newName: "Size");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "MediaAssets",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_MediaAssets_OwnerUserId_CreatedAtUtc",
                table: "MediaAssets",
                newName: "IX_MediaAssets_OwnerUserId_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_MediaAssets_ObjectKey",
                table: "MediaAssets",
                newName: "IX_MediaAssets_ObjectName");
        }
    }
}
