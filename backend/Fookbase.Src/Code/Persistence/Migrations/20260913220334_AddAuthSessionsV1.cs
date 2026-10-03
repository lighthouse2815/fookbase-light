using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthSessionsV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuthSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_SessionId_ExpiresAt",
                table: "RefreshTokens",
                columns: new[] { "SessionId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthSessions_UserId_ExpiresAtUtc",
                table: "AuthSessions",
                columns: new[] { "UserId", "ExpiresAtUtc" });

            migrationBuilder.Sql("UPDATE \"RefreshTokens\" SET \"RevokedAt\" = NOW() WHERE \"SessionId\" IS NULL AND \"RevokedAt\" IS NULL AND \"ExpiresAt\" > NOW();");

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_AuthSessions_SessionId",
                table: "RefreshTokens",
                column: "SessionId",
                principalTable: "AuthSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_AuthSessions_SessionId",
                table: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "AuthSessions");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_SessionId_ExpiresAt",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "RefreshTokens");
        }
    }
}
