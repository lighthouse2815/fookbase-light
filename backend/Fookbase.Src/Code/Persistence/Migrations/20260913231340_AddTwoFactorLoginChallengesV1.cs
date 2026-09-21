using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoFactorLoginChallengesV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TwoFactorLoginChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwoFactorLoginChallenges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TwoFactorLoginChallenges_UserId_ExpiresAtUtc",
                table: "TwoFactorLoginChallenges",
                columns: new[] { "UserId", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TwoFactorLoginChallenges");
        }
    }
}
