using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitContentMentions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommentMentions",
                columns: table => new
                {
                    CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartIndex = table.Column<int>(type: "integer", nullable: false),
                    MentionedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Length = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentMentions", x => new { x.CommentId, x.StartIndex });
                    table.ForeignKey(
                        name: "FK_CommentMentions_AspNetUsers_MentionedUserId",
                        column: x => x.MentionedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommentMentions_Comments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "Comments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostMentions",
                columns: table => new
                {
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartIndex = table.Column<int>(type: "integer", nullable: false),
                    MentionedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Length = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostMentions", x => new { x.PostId, x.StartIndex });
                    table.ForeignKey(
                        name: "FK_PostMentions_AspNetUsers_MentionedUserId",
                        column: x => x.MentionedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostMentions_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommentMentions_CommentId_MentionedUserId",
                table: "CommentMentions",
                columns: new[] { "CommentId", "MentionedUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommentMentions_MentionedUserId",
                table: "CommentMentions",
                column: "MentionedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PostMentions_MentionedUserId",
                table: "PostMentions",
                column: "MentionedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PostMentions_PostId_MentionedUserId",
                table: "PostMentions",
                columns: new[] { "PostId", "MentionedUserId" });

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "ContentMentions" AS mention
                        WHERE mention."SourceType" NOT IN (0, 1)
                           OR (mention."SourceType" = 0 AND NOT EXISTS (
                               SELECT 1 FROM "Posts" WHERE "Id" = mention."SourceId"))
                           OR (mention."SourceType" = 1 AND NOT EXISTS (
                               SELECT 1 FROM "Comments" WHERE "Id" = mention."SourceId"))
                    ) THEN
                        RAISE EXCEPTION 'Cannot split ContentMentions: unknown source type or missing post/comment source.';
                    END IF;
                END $$;

                INSERT INTO "PostMentions" ("PostId", "StartIndex", "MentionedUserId", "Length")
                SELECT "SourceId", "StartIndex", "MentionedUserId", "Length"
                FROM "ContentMentions" WHERE "SourceType" = 0;

                INSERT INTO "CommentMentions" ("CommentId", "StartIndex", "MentionedUserId", "Length")
                SELECT "SourceId", "StartIndex", "MentionedUserId", "Length"
                FROM "ContentMentions" WHERE "SourceType" = 1;
                """);

            migrationBuilder.DropTable(
                name: "ContentMentions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentMentions",
                columns: table => new
                {
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartIndex = table.Column<int>(type: "integer", nullable: false),
                    MentionedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Length = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentMentions", x => new { x.SourceType, x.SourceId, x.StartIndex });
                    table.ForeignKey(
                        name: "FK_ContentMentions_AspNetUsers_MentionedUserId",
                        column: x => x.MentionedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentMentions_MentionedUserId",
                table: "ContentMentions",
                column: "MentionedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentMentions_SourceType_SourceId_MentionedUserId",
                table: "ContentMentions",
                columns: new[] { "SourceType", "SourceId", "MentionedUserId" });

            migrationBuilder.Sql("""
                INSERT INTO "ContentMentions" ("SourceType", "SourceId", "StartIndex", "MentionedUserId", "Length")
                SELECT 0, "PostId", "StartIndex", "MentionedUserId", "Length" FROM "PostMentions"
                UNION ALL
                SELECT 1, "CommentId", "StartIndex", "MentionedUserId", "Length" FROM "CommentMentions";
                """);

            migrationBuilder.DropTable(
                name: "CommentMentions");

            migrationBuilder.DropTable(
                name: "PostMentions");
        }
    }
}
