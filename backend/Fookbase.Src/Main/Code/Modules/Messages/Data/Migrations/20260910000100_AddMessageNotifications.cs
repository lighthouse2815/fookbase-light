using System;
using Fookbase.Api.Modules.Messages.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Modules.Messages.Data.Migrations;

[DbContext(typeof(MessagesDbContext))]
[Migration("20260910000100_AddMessageNotifications")]
public partial class AddMessageNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MessageNotifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ReadAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_MessageNotifications", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_MessageNotifications_MessageId",
            table: "MessageNotifications",
            column: "MessageId",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_MessageNotifications_RecipientUserId_ReadAtUtc_CreatedAtUtc",
            table: "MessageNotifications",
            columns: new[] { "RecipientUserId", "ReadAtUtc", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MessageNotifications");
    }
}
