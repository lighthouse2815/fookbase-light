using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_AlbumMedia_MediaAssets_MediaId",
                table: "AlbumMedia",
                column: "MediaId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumMedia_PhotoAlbums_AlbumId",
                table: "AlbumMedia",
                column: "AlbumId",
                principalTable: "PhotoAlbums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PhotoAlbums_AspNetUsers_OwnerUserId",
                table: "PhotoAlbums",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlbumMedia_MediaAssets_MediaId",
                table: "AlbumMedia");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumMedia_PhotoAlbums_AlbumId",
                table: "AlbumMedia");

            migrationBuilder.DropForeignKey(
                name: "FK_PhotoAlbums_AspNetUsers_OwnerUserId",
                table: "PhotoAlbums");
        }
    }
}
