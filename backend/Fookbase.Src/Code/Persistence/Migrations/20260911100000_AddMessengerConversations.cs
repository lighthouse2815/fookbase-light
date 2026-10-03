using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations;

[DbContext(typeof(FookbaseDbContext))]
[Migration("20260911100000_AddMessengerConversations")]
public partial class AddMessengerConversations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_Conversations_CanonicalPair",
            table: "Conversations");

        migrationBuilder.DropIndex(
            name: "IX_Conversations_UserId1_LastMessageAtUtc",
            table: "Conversations");

        migrationBuilder.DropIndex(
            name: "IX_Conversations_UserId2_LastMessageAtUtc",
            table: "Conversations");

        migrationBuilder.DropIndex(
            name: "IX_Messages_ConversationId_ReadAtUtc",
            table: "Messages");

        migrationBuilder.AddColumn<Guid>(
            name: "PhotoMediaId",
            table: "Conversations",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Title",
            table: "Conversations",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Type",
            table: "Conversations",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AlterColumn<Guid>(
            name: "UserId2",
            table: "Conversations",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.AlterColumn<Guid>(
            name: "UserId1",
            table: "Conversations",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeletedAtUtc",
            table: "Messages",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "EditedAtUtc",
            table: "Messages",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ReplyToMessageId",
            table: "Messages",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Type",
            table: "Messages",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AlterColumn<string>(
            name: "Content",
            table: "Messages",
            type: "character varying(5000)",
            maxLength: 5000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(5000)",
            oldMaxLength: 5000);

        migrationBuilder.CreateTable(
            name: "ConversationParticipants",
            columns: table => new
            {
                ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Role = table.Column<int>(type: "integer", nullable: false),
                JoinedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LeftAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastReadMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                LastReadMessageCreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastReadAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastDeliveredMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                MutedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ArchivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Nickname = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_ConversationParticipants", x => new { x.ConversationId, x.UserId }));

        migrationBuilder.CreateTable(
            name: "MessageAttachments",
            columns: table => new
            {
                MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MessageAttachments", x => new { x.MessageId, x.MediaId });
                table.ForeignKey(
                    name: "FK_MessageAttachments_Messages_MessageId",
                    column: x => x.MessageId,
                    principalTable: "Messages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "MessageReactions",
            columns: table => new
            {
                MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MessageReactions", x => new { x.MessageId, x.UserId });
                table.ForeignKey(
                    name: "FK_MessageReactions_Messages_MessageId",
                    column: x => x.MessageId,
                    principalTable: "Messages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        // Existing Direct rows retain their IDs, message IDs, timestamps and cursor state.
        migrationBuilder.Sql("""
            INSERT INTO "ConversationParticipants" (
                "ConversationId", "UserId", "Role", "JoinedAtUtc", "LeftAtUtc",
                "LastReadMessageId", "LastReadMessageCreatedAtUtc", "LastReadAtUtc",
                "LastDeliveredMessageId", "MutedUntilUtc", "ArchivedAtUtc", "Nickname")
            SELECT c."Id", c."UserId1", 2, c."CreatedAtUtc", NULL,
                   r."LastReadMessageId", r."LastReadMessageCreatedAtUtc", r."LastReadAtUtc",
                   NULL, NULL, NULL, NULL
            FROM "Conversations" c
            LEFT JOIN "ConversationReadCursors" r ON r."ConversationId" = c."Id" AND r."UserId" = c."UserId1"
            WHERE c."Type" = 0 AND c."UserId1" IS NOT NULL
            ON CONFLICT ("ConversationId", "UserId") DO NOTHING;

            INSERT INTO "ConversationParticipants" (
                "ConversationId", "UserId", "Role", "JoinedAtUtc", "LeftAtUtc",
                "LastReadMessageId", "LastReadMessageCreatedAtUtc", "LastReadAtUtc",
                "LastDeliveredMessageId", "MutedUntilUtc", "ArchivedAtUtc", "Nickname")
            SELECT c."Id", c."UserId2", 2, c."CreatedAtUtc", NULL,
                   r."LastReadMessageId", r."LastReadMessageCreatedAtUtc", r."LastReadAtUtc",
                   NULL, NULL, NULL, NULL
            FROM "Conversations" c
            LEFT JOIN "ConversationReadCursors" r ON r."ConversationId" = c."Id" AND r."UserId" = c."UserId2"
            WHERE c."Type" = 0 AND c."UserId2" IS NOT NULL
            ON CONFLICT ("ConversationId", "UserId") DO NOTHING;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_ConversationParticipants_ConversationId_UserId",
            table: "ConversationParticipants",
            columns: new[] { "ConversationId", "UserId" });

        migrationBuilder.CreateIndex(
            name: "IX_ConversationParticipants_UserId_ConversationId",
            table: "ConversationParticipants",
            columns: new[] { "UserId", "ConversationId" });

        migrationBuilder.CreateIndex(
            name: "IX_Conversations_LastMessageAtUtc_Id",
            table: "Conversations",
            columns: new[] { "LastMessageAtUtc", "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_MessageAttachments_MediaId",
            table: "MessageAttachments",
            column: "MediaId");

        migrationBuilder.CreateIndex(
            name: "IX_MessageAttachments_MessageId_SortOrder",
            table: "MessageAttachments",
            columns: new[] { "MessageId", "SortOrder" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MessageReactions_MessageId_Type",
            table: "MessageReactions",
            columns: new[] { "MessageId", "Type" });

        migrationBuilder.CreateIndex(
            name: "IX_Messages_ReplyToMessageId",
            table: "Messages",
            column: "ReplyToMessageId");

        migrationBuilder.AddForeignKey(
            name: "FK_Messages_Messages_ReplyToMessageId",
            table: "Messages",
            column: "ReplyToMessageId",
            principalTable: "Messages",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Messages_Messages_ReplyToMessageId", table: "Messages");
        migrationBuilder.DropTable(name: "ConversationParticipants");
        migrationBuilder.DropTable(name: "MessageAttachments");
        migrationBuilder.DropTable(name: "MessageReactions");
        migrationBuilder.DropIndex(name: "IX_Conversations_LastMessageAtUtc_Id", table: "Conversations");
        migrationBuilder.DropIndex(name: "IX_Messages_ReplyToMessageId", table: "Messages");
        migrationBuilder.DropColumn(name: "PhotoMediaId", table: "Conversations");
        migrationBuilder.DropColumn(name: "Title", table: "Conversations");
        migrationBuilder.DropColumn(name: "Type", table: "Conversations");
        migrationBuilder.DropColumn(name: "DeletedAtUtc", table: "Messages");
        migrationBuilder.DropColumn(name: "EditedAtUtc", table: "Messages");
        migrationBuilder.DropColumn(name: "ReplyToMessageId", table: "Messages");
        migrationBuilder.DropColumn(name: "Type", table: "Messages");
        migrationBuilder.AlterColumn<string>(name: "Content", table: "Messages", type: "character varying(5000)", maxLength: 5000, nullable: false, defaultValue: "", oldClrType: typeof(string), oldType: "character varying(5000)", oldMaxLength: 5000, oldNullable: true);
        migrationBuilder.AlterColumn<Guid>(name: "UserId2", table: "Conversations", type: "uuid", nullable: false, defaultValue: Guid.Empty, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
        migrationBuilder.AlterColumn<Guid>(name: "UserId1", table: "Conversations", type: "uuid", nullable: false, defaultValue: Guid.Empty, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
        migrationBuilder.CreateIndex(name: "IX_Conversations_UserId1_LastMessageAtUtc", table: "Conversations", columns: new[] { "UserId1", "LastMessageAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_Conversations_UserId2_LastMessageAtUtc", table: "Conversations", columns: new[] { "UserId2", "LastMessageAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_Messages_ConversationId_ReadAtUtc", table: "Messages", columns: new[] { "ConversationId", "ReadAtUtc" });
        migrationBuilder.AddCheckConstraint(name: "CK_Conversations_CanonicalPair", table: "Conversations", sql: "\"UserId1\" < \"UserId2\"");
    }
}
