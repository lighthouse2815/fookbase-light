using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Code.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Events_CoverMediaId",
                table: "Events",
                column: "CoverMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_CreatedByUserId",
                table: "Events",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EventInvitations_InviterUserId",
                table: "EventInvitations",
                column: "InviterUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_EventCoverMediaReferences_Events_EventId",
                table: "EventCoverMediaReferences",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EventCoverMediaReferences_MediaAssets_MediaId",
                table: "EventCoverMediaReferences",
                column: "MediaId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EventInvitations_AspNetUsers_InviteeUserId",
                table: "EventInvitations",
                column: "InviteeUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EventInvitations_AspNetUsers_InviterUserId",
                table: "EventInvitations",
                column: "InviterUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EventInvitations_Events_EventId",
                table: "EventInvitations",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EventParticipants_AspNetUsers_UserId",
                table: "EventParticipants",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EventParticipants_Events_EventId",
                table: "EventParticipants",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Events_AspNetUsers_CreatedByUserId",
                table: "Events",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Events_MediaAssets_CoverMediaId",
                table: "Events",
                column: "CoverMediaId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventCoverMediaReferences_Events_EventId",
                table: "EventCoverMediaReferences");

            migrationBuilder.DropForeignKey(
                name: "FK_EventCoverMediaReferences_MediaAssets_MediaId",
                table: "EventCoverMediaReferences");

            migrationBuilder.DropForeignKey(
                name: "FK_EventInvitations_AspNetUsers_InviteeUserId",
                table: "EventInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_EventInvitations_AspNetUsers_InviterUserId",
                table: "EventInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_EventInvitations_Events_EventId",
                table: "EventInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_EventParticipants_AspNetUsers_UserId",
                table: "EventParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_EventParticipants_Events_EventId",
                table: "EventParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_AspNetUsers_CreatedByUserId",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_MediaAssets_CoverMediaId",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_CoverMediaId",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_CreatedByUserId",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_EventInvitations_InviterUserId",
                table: "EventInvitations");
        }
    }
}
