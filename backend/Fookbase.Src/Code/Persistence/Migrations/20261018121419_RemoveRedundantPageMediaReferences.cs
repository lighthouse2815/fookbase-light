using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRedundantPageMediaReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PageMediaReferences");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                    table.ForeignKey(
                        name: "FK_PageMediaReferences_MediaAssets_MediaId",
                        column: x => x.MediaId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PageMediaReferences_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PageMediaReferences_MediaId",
                table: "PageMediaReferences",
                column: "MediaId");

            migrationBuilder.Sql("""
                INSERT INTO "PageMediaReferences" ("PageId", "Slot", "MediaId", "AttachedAtUtc")
                SELECT "Id", 0, "AvatarMediaId", COALESCE("UpdatedAtUtc", "CreatedAtUtc")
                FROM "Pages" WHERE "AvatarMediaId" IS NOT NULL AND "DeletedAtUtc" IS NULL
                UNION ALL
                SELECT "Id", 1, "CoverMediaId", COALESCE("UpdatedAtUtc", "CreatedAtUtc")
                FROM "Pages" WHERE "CoverMediaId" IS NOT NULL AND "DeletedAtUtc" IS NULL;
                """);
        }
    }
}
