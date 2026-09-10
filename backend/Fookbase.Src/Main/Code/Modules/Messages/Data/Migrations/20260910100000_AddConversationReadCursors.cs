using System;
using Fookbase.Api.Modules.Messages.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Modules.Messages.Data.Migrations;

[DbContext(typeof(MessagesDbContext))]
[Migration("20260910100000_AddConversationReadCursors")]
public partial class AddConversationReadCursors : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ConversationReadCursors",
            columns: table => new
            {
                ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                LastReadMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                LastReadMessageCreatedAtUtc = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true),
                LastReadAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ConversationReadCursors", x => new { x.ConversationId, x.UserId });
            });

        migrationBuilder.CreateIndex(
            name: "IX_ConversationReadCursors_UserId_ConversationId",
            table: "ConversationReadCursors",
            columns: new[] { "UserId", "ConversationId" });

        migrationBuilder.Sql("""
            INSERT INTO "ConversationReadCursors" (
                "ConversationId",
                "UserId",
                "LastReadMessageId",
                "LastReadMessageCreatedAtUtc",
                "LastReadAtUtc")
            SELECT DISTINCT ON (
                    message."ConversationId",
                    CASE
                        WHEN message."SenderUserId" = conversation."UserId1" THEN conversation."UserId2"
                        ELSE conversation."UserId1"
                    END)
                message."ConversationId",
                CASE
                    WHEN message."SenderUserId" = conversation."UserId1" THEN conversation."UserId2"
                    ELSE conversation."UserId1"
                END,
                message."Id",
                message."CreatedAtUtc",
                message."ReadAtUtc"
            FROM "Messages" AS message
            INNER JOIN "Conversations" AS conversation
                ON message."ConversationId" = conversation."Id"
            WHERE message."ReadAtUtc" IS NOT NULL
            ORDER BY
                message."ConversationId",
                CASE
                    WHEN message."SenderUserId" = conversation."UserId1" THEN conversation."UserId2"
                    ELSE conversation."UserId1"
                END,
                message."CreatedAtUtc" DESC,
                message."Id" DESC;
            """);

        migrationBuilder.DropIndex(
            name: "IX_Messages_ConversationId_CreatedAtUtc",
            table: "Messages");
        migrationBuilder.CreateIndex(
            name: "IX_Messages_ConversationId_CreatedAtUtc_Id",
            table: "Messages",
            columns: new[] { "ConversationId", "CreatedAtUtc", "Id" });

        migrationBuilder.DropIndex(
            name: "IX_MessageNotifications_MessageId",
            table: "MessageNotifications");
        migrationBuilder.CreateIndex(
            name: "IX_MessageNotifications_RecipientUserId_MessageId",
            table: "MessageNotifications",
            columns: new[] { "RecipientUserId", "MessageId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Messages_ConversationId_CreatedAtUtc_Id",
            table: "Messages");
        migrationBuilder.CreateIndex(
            name: "IX_Messages_ConversationId_CreatedAtUtc",
            table: "Messages",
            columns: new[] { "ConversationId", "CreatedAtUtc" });

        migrationBuilder.DropIndex(
            name: "IX_MessageNotifications_RecipientUserId_MessageId",
            table: "MessageNotifications");
        migrationBuilder.CreateIndex(
            name: "IX_MessageNotifications_MessageId",
            table: "MessageNotifications",
            column: "MessageId",
            unique: true);

        migrationBuilder.DropTable(name: "ConversationReadCursors");
    }
}
