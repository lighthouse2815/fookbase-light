using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRedundantPostMediaReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaReferences");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                    table.ForeignKey(
                        name: "FK_MediaReferences_MediaAssets_MediaId",
                        column: x => x.MediaId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MediaReferences_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaReferences_PostId",
                table: "MediaReferences",
                column: "PostId");

            migrationBuilder.Sql("""
                INSERT INTO "MediaReferences" ("MediaId", "PostId", "AttachedAtUtc")
                SELECT media."MediaId", media."PostId", post."CreatedAtUtc"
                FROM "PostMedia" AS media
                INNER JOIN "Posts" AS post ON post."Id" = media."PostId";
                """);
        }
    }
}
