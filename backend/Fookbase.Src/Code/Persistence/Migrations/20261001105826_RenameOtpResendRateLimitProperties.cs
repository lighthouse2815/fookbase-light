using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Code.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameOtpResendRateLimitProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "WindowStartedAtUtc",
                table: "RegistrationChallenges",
                newName: "SendLimitWindowStartedAtUtc");

            migrationBuilder.RenameColumn(
                name: "ResendAvailableAtUtc",
                table: "RegistrationChallenges",
                newName: "NextResendAllowedAtUtc");

            migrationBuilder.RenameColumn(
                name: "WindowStartedAtUtc",
                table: "PasswordResetOtps",
                newName: "SendLimitWindowStartedAtUtc");

            migrationBuilder.RenameColumn(
                name: "ResendAvailableAtUtc",
                table: "PasswordResetOtps",
                newName: "NextResendAllowedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SendLimitWindowStartedAtUtc",
                table: "RegistrationChallenges",
                newName: "WindowStartedAtUtc");

            migrationBuilder.RenameColumn(
                name: "NextResendAllowedAtUtc",
                table: "RegistrationChallenges",
                newName: "ResendAvailableAtUtc");

            migrationBuilder.RenameColumn(
                name: "SendLimitWindowStartedAtUtc",
                table: "PasswordResetOtps",
                newName: "WindowStartedAtUtc");

            migrationBuilder.RenameColumn(
                name: "NextResendAllowedAtUtc",
                table: "PasswordResetOtps",
                newName: "ResendAvailableAtUtc");
        }
    }
}
