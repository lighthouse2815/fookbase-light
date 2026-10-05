using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ModerationActions_ModeratorUserId",
                table: "ModerationActions",
                column: "ModeratorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationActions_ReportId",
                table: "ModerationActions",
                column: "ReportId");

            migrationBuilder.AddForeignKey(
                name: "FK_ModerationActions_AspNetUsers_ModeratorUserId",
                table: "ModerationActions",
                column: "ModeratorUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ModerationActions_AspNetUsers_SubjectUserId",
                table: "ModerationActions",
                column: "SubjectUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ModerationActions_ContentReports_ReportId",
                table: "ModerationActions",
                column: "ReportId",
                principalTable: "ContentReports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserModerationStates_AspNetUsers_UserId",
                table: "UserModerationStates",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModerationActions_AspNetUsers_ModeratorUserId",
                table: "ModerationActions");

            migrationBuilder.DropForeignKey(
                name: "FK_ModerationActions_AspNetUsers_SubjectUserId",
                table: "ModerationActions");

            migrationBuilder.DropForeignKey(
                name: "FK_ModerationActions_ContentReports_ReportId",
                table: "ModerationActions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserModerationStates_AspNetUsers_UserId",
                table: "UserModerationStates");

            migrationBuilder.DropIndex(
                name: "IX_ModerationActions_ModeratorUserId",
                table: "ModerationActions");

            migrationBuilder.DropIndex(
                name: "IX_ModerationActions_ReportId",
                table: "ModerationActions");
        }
    }
}
