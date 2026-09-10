using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Modules.Messages.Data.Migrations;

public partial class InitialMessages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Conversations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId1 = table.Column<Guid>(type: "uuid", nullable: false),
                UserId2 = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastMessageAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Conversations", x => x.Id);
                table.CheckConstraint("CK_Conversations_CanonicalPair", "\"UserId1\" < \"UserId2\"");
            });

        migrationBuilder.CreateTable(
            name: "Messages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                SenderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                Content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ReadAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Messages", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_Conversations_UserId1_LastMessageAtUtc",
            table: "Conversations",
            columns: new[] { "UserId1", "LastMessageAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_Conversations_UserId1_UserId2",
            table: "Conversations",
            columns: new[] { "UserId1", "UserId2" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_Conversations_UserId2_LastMessageAtUtc",
            table: "Conversations",
            columns: new[] { "UserId2", "LastMessageAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_Messages_ConversationId_CreatedAtUtc",
            table: "Messages",
            columns: new[] { "ConversationId", "CreatedAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_Messages_ConversationId_ReadAtUtc",
            table: "Messages",
            columns: new[] { "ConversationId", "ReadAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Messages");
        migrationBuilder.DropTable(name: "Conversations");
    }
}
