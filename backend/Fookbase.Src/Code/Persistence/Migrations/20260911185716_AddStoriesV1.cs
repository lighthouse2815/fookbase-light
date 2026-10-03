using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoriesV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StoryId",
                table: "Messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Stories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Caption = table.Column<string>(type: "character varying(2200)", maxLength: 2200, nullable: true),
                    Privacy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoryMediaReferences",
                columns: table => new
                {
                    StoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttachedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryMediaReferences", x => x.StoryId);
                    table.ForeignKey(
                        name: "FK_StoryMediaReferences_MediaAssets_MediaId",
                        column: x => x.MediaId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoryMediaReferences_Stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "Stories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoryReactions",
                columns: table => new
                {
                    StoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryReactions", x => new { x.StoryId, x.UserId });
                    table.ForeignKey(
                        name: "FK_StoryReactions_Stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "Stories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoryViews",
                columns: table => new
                {
                    StoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryViews", x => new { x.StoryId, x.ViewerUserId });
                    table.ForeignKey(
                        name: "FK_StoryViews_Stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "Stories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_StoryId",
                table: "Messages",
                column: "StoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Stories_AuthorUserId_ExpiresAtUtc_CreatedAtUtc_Id",
                table: "Stories",
                columns: new[] { "AuthorUserId", "ExpiresAtUtc", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Stories_ExpiresAtUtc_CreatedAtUtc",
                table: "Stories",
                columns: new[] { "ExpiresAtUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Stories_MediaId",
                table: "Stories",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryMediaReferences_MediaId",
                table: "StoryMediaReferences",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryReactions_StoryId_CreatedAtUtc",
                table: "StoryReactions",
                columns: new[] { "StoryId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryViews_StoryId_ViewedAtUtc_ViewerUserId",
                table: "StoryViews",
                columns: new[] { "StoryId", "ViewedAtUtc", "ViewerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryViews_ViewerUserId_ViewedAtUtc",
                table: "StoryViews",
                columns: new[] { "ViewerUserId", "ViewedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Stories_StoryId",
                table: "Messages",
                column: "StoryId",
                principalTable: "Stories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Stories_StoryId",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "StoryMediaReferences");

            migrationBuilder.DropTable(
                name: "StoryReactions");

            migrationBuilder.DropTable(
                name: "StoryViews");

            migrationBuilder.DropTable(
                name: "Stories");

            migrationBuilder.DropIndex(
                name: "IX_Messages_StoryId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "StoryId",
                table: "Messages");
        }
    }
}
