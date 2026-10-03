using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReelsVideoProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PostType",
                table: "Posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "DurationMs",
                table: "MediaAssets",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "MediaAssets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PosterObjectKey",
                table: "MediaAssets",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProcessedAtUtc",
                table: "MediaAssets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessedObjectKey",
                table: "MediaAssets",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingError",
                table: "MediaAssets",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "MediaAssets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MediaProcessingJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaProcessingJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReelViews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReelPostId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WatchDurationMs = table.Column<int>(type: "integer", nullable: false),
                    Completed = table.Column<bool>(type: "boolean", nullable: false),
                    Replayed = table.Column<bool>(type: "boolean", nullable: false),
                    ViewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReelViews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_PostType_AuthorUserId_CreatedAtUtc_Id",
                table: "Posts",
                columns: new[] { "PostType", "AuthorUserId", "CreatedAtUtc", "Id" },
                filter: "\"DeletedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_PostType_CreatedAtUtc_Id",
                table: "Posts",
                columns: new[] { "PostType", "CreatedAtUtc", "Id" },
                filter: "\"DeletedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_Status_CreatedAtUtc",
                table: "MediaAssets",
                columns: new[] { "Status", "CreatedAtUtc" },
                filter: "\"Status\" = 4");

            migrationBuilder.CreateIndex(
                name: "IX_MediaProcessingJobs_MediaId",
                table: "MediaProcessingJobs",
                column: "MediaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaProcessingJobs_Status_NextAttemptAtUtc_CreatedAtUtc",
                table: "MediaProcessingJobs",
                columns: new[] { "Status", "NextAttemptAtUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReelViews_ReelPostId_Completed_ViewedAtUtc",
                table: "ReelViews",
                columns: new[] { "ReelPostId", "Completed", "ViewedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReelViews_ReelPostId_ViewedAtUtc",
                table: "ReelViews",
                columns: new[] { "ReelPostId", "ViewedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReelViews_ViewerUserId_ViewedAtUtc",
                table: "ReelViews",
                columns: new[] { "ViewerUserId", "ViewedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaProcessingJobs");

            migrationBuilder.DropTable(
                name: "ReelViews");

            migrationBuilder.DropIndex(
                name: "IX_Posts_PostType_AuthorUserId_CreatedAtUtc_Id",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Posts_PostType_CreatedAtUtc_Id",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_MediaAssets_Status_CreatedAtUtc",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "PostType",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "DurationMs",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "PosterObjectKey",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "ProcessedAtUtc",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "ProcessedObjectKey",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "ProcessingError",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "MediaAssets");
        }
    }
}
