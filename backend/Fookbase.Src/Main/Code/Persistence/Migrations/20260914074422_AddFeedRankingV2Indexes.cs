using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Code.Persistence.Migrations
{
    /// <inheritdoc />
    // This migration follows Moderation V1 in the production sequence.
    public partial class AddFeedRankingV2Indexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PostShares_SharingUserId_DeletedAtUtc_CreatedAtUtc_Original~",
                table: "PostShares",
                columns: new[] { "SharingUserId", "DeletedAtUtc", "CreatedAtUtc", "OriginalPostId" });

            migrationBuilder.CreateIndex(
                name: "IX_PostReactions_UserId_CreatedAtUtc_PostId",
                table: "PostReactions",
                columns: new[] { "UserId", "CreatedAtUtc", "PostId" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_AuthorUserId_DeletedAtUtc_CreatedAtUtc_PostId",
                table: "Comments",
                columns: new[] { "AuthorUserId", "DeletedAtUtc", "CreatedAtUtc", "PostId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PostShares_SharingUserId_DeletedAtUtc_CreatedAtUtc_Original~",
                table: "PostShares");

            migrationBuilder.DropIndex(
                name: "IX_PostReactions_UserId_CreatedAtUtc_PostId",
                table: "PostReactions");

            migrationBuilder.DropIndex(
                name: "IX_Comments_AuthorUserId_DeletedAtUtc_CreatedAtUtc_PostId",
                table: "Comments");
        }
    }
}
