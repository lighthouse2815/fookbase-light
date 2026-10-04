using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPageRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Pages_AvatarMediaId",
                table: "Pages",
                column: "AvatarMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pages_CoverMediaId",
                table: "Pages",
                column: "CoverMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_PageRoleInvitations_InviterUserId",
                table: "PageRoleInvitations",
                column: "InviterUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PageFollowers_AspNetUsers_UserId",
                table: "PageFollowers",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PageFollowers_Pages_PageId",
                table: "PageFollowers",
                column: "PageId",
                principalTable: "Pages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PageMediaReferences_MediaAssets_MediaId",
                table: "PageMediaReferences",
                column: "MediaId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PageMediaReferences_Pages_PageId",
                table: "PageMediaReferences",
                column: "PageId",
                principalTable: "Pages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PageMembers_AspNetUsers_UserId",
                table: "PageMembers",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PageMembers_Pages_PageId",
                table: "PageMembers",
                column: "PageId",
                principalTable: "Pages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PageRoleInvitations_AspNetUsers_InviteeUserId",
                table: "PageRoleInvitations",
                column: "InviteeUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PageRoleInvitations_AspNetUsers_InviterUserId",
                table: "PageRoleInvitations",
                column: "InviterUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PageRoleInvitations_Pages_PageId",
                table: "PageRoleInvitations",
                column: "PageId",
                principalTable: "Pages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pages_AspNetUsers_CreatedByUserId",
                table: "Pages",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pages_MediaAssets_AvatarMediaId",
                table: "Pages",
                column: "AvatarMediaId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pages_MediaAssets_CoverMediaId",
                table: "Pages",
                column: "CoverMediaId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PageFollowers_AspNetUsers_UserId",
                table: "PageFollowers");

            migrationBuilder.DropForeignKey(
                name: "FK_PageFollowers_Pages_PageId",
                table: "PageFollowers");

            migrationBuilder.DropForeignKey(
                name: "FK_PageMediaReferences_MediaAssets_MediaId",
                table: "PageMediaReferences");

            migrationBuilder.DropForeignKey(
                name: "FK_PageMediaReferences_Pages_PageId",
                table: "PageMediaReferences");

            migrationBuilder.DropForeignKey(
                name: "FK_PageMembers_AspNetUsers_UserId",
                table: "PageMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_PageMembers_Pages_PageId",
                table: "PageMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_PageRoleInvitations_AspNetUsers_InviteeUserId",
                table: "PageRoleInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_PageRoleInvitations_AspNetUsers_InviterUserId",
                table: "PageRoleInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_PageRoleInvitations_Pages_PageId",
                table: "PageRoleInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_Pages_AspNetUsers_CreatedByUserId",
                table: "Pages");

            migrationBuilder.DropForeignKey(
                name: "FK_Pages_MediaAssets_AvatarMediaId",
                table: "Pages");

            migrationBuilder.DropForeignKey(
                name: "FK_Pages_MediaAssets_CoverMediaId",
                table: "Pages");

            migrationBuilder.DropIndex(
                name: "IX_Pages_AvatarMediaId",
                table: "Pages");

            migrationBuilder.DropIndex(
                name: "IX_Pages_CoverMediaId",
                table: "Pages");

            migrationBuilder.DropIndex(
                name: "IX_PageRoleInvitations_InviterUserId",
                table: "PageRoleInvitations");
        }
    }
}
