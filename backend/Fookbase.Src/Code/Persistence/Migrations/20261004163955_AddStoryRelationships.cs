using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoryRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StoryReactions_UserId",
                table: "StoryReactions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Stories_AspNetUsers_AuthorUserId",
                table: "Stories",
                column: "AuthorUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Stories_MediaAssets_MediaId",
                table: "Stories",
                column: "MediaId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StoryReactions_AspNetUsers_UserId",
                table: "StoryReactions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StoryViews_AspNetUsers_ViewerUserId",
                table: "StoryViews",
                column: "ViewerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stories_AspNetUsers_AuthorUserId",
                table: "Stories");

            migrationBuilder.DropForeignKey(
                name: "FK_Stories_MediaAssets_MediaId",
                table: "Stories");

            migrationBuilder.DropForeignKey(
                name: "FK_StoryReactions_AspNetUsers_UserId",
                table: "StoryReactions");

            migrationBuilder.DropForeignKey(
                name: "FK_StoryViews_AspNetUsers_ViewerUserId",
                table: "StoryViews");

            migrationBuilder.DropIndex(
                name: "IX_StoryReactions_UserId",
                table: "StoryReactions");
        }
    }
}
