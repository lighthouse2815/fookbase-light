using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Code.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameExternalLoginCompletionToTicket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "ExternalLoginCompletions",
                newName: "ExternalLoginTickets");

            migrationBuilder.RenameIndex(
                name: "IX_ExternalLoginCompletions_CodeHash_ExpiresAtUtc",
                table: "ExternalLoginTickets",
                newName: "IX_ExternalLoginTickets_CodeHash_ExpiresAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_ExternalLoginCompletions_UserId",
                table: "ExternalLoginTickets",
                newName: "IX_ExternalLoginTickets_UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_ExternalLoginTickets_CodeHash_ExpiresAtUtc",
                table: "ExternalLoginTickets",
                newName: "IX_ExternalLoginCompletions_CodeHash_ExpiresAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_ExternalLoginTickets_UserId",
                table: "ExternalLoginTickets",
                newName: "IX_ExternalLoginCompletions_UserId");

            migrationBuilder.RenameTable(
                name: "ExternalLoginTickets",
                newName: "ExternalLoginCompletions");
        }
    }
}
