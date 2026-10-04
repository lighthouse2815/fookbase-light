using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Groups_CoverMediaId",
                table: "Groups",
                column: "CoverMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupJoinRequests_RequesterUserId",
                table: "GroupJoinRequests",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupJoinRequests_RespondedByUserId",
                table: "GroupJoinRequests",
                column: "RespondedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvites_InviterUserId",
                table: "GroupInvites",
                column: "InviterUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupCoverMediaReferences_Groups_GroupId",
                table: "GroupCoverMediaReferences",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupCoverMediaReferences_MediaAssets_MediaId",
                table: "GroupCoverMediaReferences",
                column: "MediaId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupInvites_AspNetUsers_InviteeUserId",
                table: "GroupInvites",
                column: "InviteeUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupInvites_AspNetUsers_InviterUserId",
                table: "GroupInvites",
                column: "InviterUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupInvites_Groups_GroupId",
                table: "GroupInvites",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupJoinRequests_AspNetUsers_RequesterUserId",
                table: "GroupJoinRequests",
                column: "RequesterUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupJoinRequests_AspNetUsers_RespondedByUserId",
                table: "GroupJoinRequests",
                column: "RespondedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupJoinRequests_Groups_GroupId",
                table: "GroupJoinRequests",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMembers_AspNetUsers_UserId",
                table: "GroupMembers",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMembers_Groups_GroupId",
                table: "GroupMembers",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupRules_Groups_GroupId",
                table: "GroupRules",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Groups_AspNetUsers_OwnerUserId",
                table: "Groups",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Groups_MediaAssets_CoverMediaId",
                table: "Groups",
                column: "CoverMediaId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupCoverMediaReferences_Groups_GroupId",
                table: "GroupCoverMediaReferences");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupCoverMediaReferences_MediaAssets_MediaId",
                table: "GroupCoverMediaReferences");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupInvites_AspNetUsers_InviteeUserId",
                table: "GroupInvites");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupInvites_AspNetUsers_InviterUserId",
                table: "GroupInvites");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupInvites_Groups_GroupId",
                table: "GroupInvites");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupJoinRequests_AspNetUsers_RequesterUserId",
                table: "GroupJoinRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupJoinRequests_AspNetUsers_RespondedByUserId",
                table: "GroupJoinRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupJoinRequests_Groups_GroupId",
                table: "GroupJoinRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupMembers_AspNetUsers_UserId",
                table: "GroupMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupMembers_Groups_GroupId",
                table: "GroupMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupRules_Groups_GroupId",
                table: "GroupRules");

            migrationBuilder.DropForeignKey(
                name: "FK_Groups_AspNetUsers_OwnerUserId",
                table: "Groups");

            migrationBuilder.DropForeignKey(
                name: "FK_Groups_MediaAssets_CoverMediaId",
                table: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_Groups_CoverMediaId",
                table: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_GroupJoinRequests_RequesterUserId",
                table: "GroupJoinRequests");

            migrationBuilder.DropIndex(
                name: "IX_GroupJoinRequests_RespondedByUserId",
                table: "GroupJoinRequests");

            migrationBuilder.DropIndex(
                name: "IX_GroupInvites_InviterUserId",
                table: "GroupInvites");
        }
    }
}
