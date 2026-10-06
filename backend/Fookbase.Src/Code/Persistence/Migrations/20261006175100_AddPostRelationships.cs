using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPostRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PostMedia_MediaId",
                table: "PostMedia",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentReactions_UserId",
                table: "CommentReactions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CommentReactions_AspNetUsers_UserId",
                table: "CommentReactions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_AspNetUsers_AuthorUserId",
                table: "Comments",
                column: "AuthorUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentReports_AspNetUsers_ReporterUserId",
                table: "ContentReports",
                column: "ReporterUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostMedia_MediaAssets_MediaId",
                table: "PostMedia",
                column: "MediaId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostReactions_AspNetUsers_UserId",
                table: "PostReactions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_AspNetUsers_AuthorUserId",
                table: "Posts",
                column: "AuthorUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostSaves_AspNetUsers_UserId",
                table: "PostSaves",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostShares_AspNetUsers_SharingUserId",
                table: "PostShares",
                column: "SharingUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommentReactions_AspNetUsers_UserId",
                table: "CommentReactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Comments_AspNetUsers_AuthorUserId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_ContentReports_AspNetUsers_ReporterUserId",
                table: "ContentReports");

            migrationBuilder.DropForeignKey(
                name: "FK_PostMedia_MediaAssets_MediaId",
                table: "PostMedia");

            migrationBuilder.DropForeignKey(
                name: "FK_PostReactions_AspNetUsers_UserId",
                table: "PostReactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Posts_AspNetUsers_AuthorUserId",
                table: "Posts");

            migrationBuilder.DropForeignKey(
                name: "FK_PostSaves_AspNetUsers_UserId",
                table: "PostSaves");

            migrationBuilder.DropForeignKey(
                name: "FK_PostShares_AspNetUsers_SharingUserId",
                table: "PostShares");

            migrationBuilder.DropIndex(
                name: "IX_PostMedia_MediaId",
                table: "PostMedia");

            migrationBuilder.DropIndex(
                name: "IX_CommentReactions_UserId",
                table: "CommentReactions");
        }
    }
}
