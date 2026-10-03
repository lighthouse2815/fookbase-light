using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Code.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenamePasswordResetOtpTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PasswordResetChallenges_AspNetUsers_UserId",
                table: "PasswordResetChallenges");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PasswordResetChallenges",
                table: "PasswordResetChallenges");

            migrationBuilder.RenameTable(
                name: "PasswordResetChallenges",
                newName: "PasswordResetOtps");

            migrationBuilder.RenameIndex(
                name: "IX_PasswordResetChallenges_UserId",
                table: "PasswordResetOtps",
                newName: "IX_PasswordResetOtps_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PasswordResetOtps",
                table: "PasswordResetOtps",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PasswordResetOtps_AspNetUsers_UserId",
                table: "PasswordResetOtps",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PasswordResetOtps_AspNetUsers_UserId",
                table: "PasswordResetOtps");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PasswordResetOtps",
                table: "PasswordResetOtps");

            migrationBuilder.RenameTable(
                name: "PasswordResetOtps",
                newName: "PasswordResetChallenges");

            migrationBuilder.RenameIndex(
                name: "IX_PasswordResetOtps_UserId",
                table: "PasswordResetChallenges",
                newName: "IX_PasswordResetChallenges_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PasswordResetChallenges",
                table: "PasswordResetChallenges",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PasswordResetChallenges_AspNetUsers_UserId",
                table: "PasswordResetChallenges",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
