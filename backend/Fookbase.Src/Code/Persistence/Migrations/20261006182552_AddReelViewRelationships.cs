using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReelViewRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_ReelViews_AspNetUsers_ViewerUserId",
                table: "ReelViews",
                column: "ViewerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReelViews_Posts_ReelPostId",
                table: "ReelViews",
                column: "ReelPostId",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReelViews_AspNetUsers_ViewerUserId",
                table: "ReelViews");

            migrationBuilder.DropForeignKey(
                name: "FK_ReelViews_Posts_ReelPostId",
                table: "ReelViews");
        }
    }
}
