using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ObjectDeletions_MediaId",
                table: "ObjectDeletions",
                column: "MediaId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaProcessingJobs_MediaAssets_MediaId",
                table: "MediaProcessingJobs",
                column: "MediaId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MediaReferences_MediaAssets_MediaId",
                table: "MediaReferences",
                column: "MediaId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MediaReferences_Posts_PostId",
                table: "MediaReferences",
                column: "PostId",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ObjectDeletions_MediaAssets_MediaId",
                table: "ObjectDeletions",
                column: "MediaId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileMediaReferences_AspNetUsers_UserId",
                table: "ProfileMediaReferences",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileMediaReferences_MediaAssets_MediaId",
                table: "ProfileMediaReferences",
                column: "MediaId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaProcessingJobs_MediaAssets_MediaId",
                table: "MediaProcessingJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaReferences_MediaAssets_MediaId",
                table: "MediaReferences");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaReferences_Posts_PostId",
                table: "MediaReferences");

            migrationBuilder.DropForeignKey(
                name: "FK_ObjectDeletions_MediaAssets_MediaId",
                table: "ObjectDeletions");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileMediaReferences_AspNetUsers_UserId",
                table: "ProfileMediaReferences");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileMediaReferences_MediaAssets_MediaId",
                table: "ProfileMediaReferences");

            migrationBuilder.DropIndex(
                name: "IX_ObjectDeletions_MediaId",
                table: "ObjectDeletions");
        }
    }
}
