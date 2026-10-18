using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRedundantConversationReadCursors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve legacy read positions and direct-chat membership before dropping the projection.
            migrationBuilder.Sql("""
                INSERT INTO "ConversationParticipants"
                    ("ConversationId", "UserId", "Role", "JoinedAtUtc",
                     "LastReadMessageId", "LastReadMessageCreatedAtUtc", "LastReadAtUtc")
                SELECT cursor."ConversationId", cursor."UserId", 2, conversation."CreatedAtUtc",
                       cursor."LastReadMessageId", cursor."LastReadMessageCreatedAtUtc", cursor."LastReadAtUtc"
                FROM "ConversationReadCursors" AS cursor
                INNER JOIN "Conversations" AS conversation ON conversation."Id" = cursor."ConversationId"
                WHERE conversation."Type" = 0
                  AND cursor."UserId" IN (conversation."UserId1", conversation."UserId2")
                ON CONFLICT ("ConversationId", "UserId") DO NOTHING;

                UPDATE "ConversationParticipants" AS participant
                SET "LastReadMessageId" = cursor."LastReadMessageId",
                    "LastReadMessageCreatedAtUtc" = cursor."LastReadMessageCreatedAtUtc",
                    "LastReadAtUtc" = cursor."LastReadAtUtc"
                FROM "ConversationReadCursors" AS cursor
                WHERE participant."ConversationId" = cursor."ConversationId"
                  AND participant."UserId" = cursor."UserId"
                  AND cursor."LastReadMessageCreatedAtUtc" IS NOT NULL
                  AND (participant."LastReadMessageCreatedAtUtc" IS NULL
                       OR cursor."LastReadMessageCreatedAtUtc" > participant."LastReadMessageCreatedAtUtc"
                       OR (cursor."LastReadMessageCreatedAtUtc" = participant."LastReadMessageCreatedAtUtc"
                           AND (participant."LastReadMessageId" IS NULL
                                OR cursor."LastReadMessageId" > participant."LastReadMessageId")));
                """);

            migrationBuilder.DropTable(
                name: "ConversationReadCursors");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationReadCursors",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastReadMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastReadAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReadMessageCreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationReadCursors", x => new { x.ConversationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ConversationReadCursors_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationReadCursors_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationReadCursors_Messages_LastReadMessageId",
                        column: x => x.LastReadMessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationReadCursors_LastReadMessageId",
                table: "ConversationReadCursors",
                column: "LastReadMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationReadCursors_UserId_ConversationId",
                table: "ConversationReadCursors",
                columns: new[] { "UserId", "ConversationId" });

            migrationBuilder.Sql("""
                INSERT INTO "ConversationReadCursors"
                    ("ConversationId", "UserId", "LastReadMessageId", "LastReadMessageCreatedAtUtc", "LastReadAtUtc")
                SELECT participant."ConversationId", participant."UserId", participant."LastReadMessageId",
                       participant."LastReadMessageCreatedAtUtc", participant."LastReadAtUtc"
                FROM "ConversationParticipants" AS participant
                INNER JOIN "Conversations" AS conversation ON conversation."Id" = participant."ConversationId"
                WHERE conversation."Type" = 0
                  AND participant."UserId" IN (conversation."UserId1", conversation."UserId2");
                """);
        }
    }
}
