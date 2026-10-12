using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnifyFriendNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LegacyFriendNotificationId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            // Both legacy types have the same persisted integer values in NotificationType.
            // Keep the shared ID and remember the legacy ID for clients with cached notifications.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE friend_notification_matches ON COMMIT DROP AS
                SELECT f."Id" AS legacy_id, n."Id" AS notification_id
                FROM "FriendNotifications" AS f
                INNER JOIN "Notifications" AS n
                    ON n."Id" = f."Id"
                   AND n."RecipientUserId" = f."RecipientUserId"
                   AND n."ActorUserId" = f."ActorUserId"
                   AND n."Type" = f."Type"
                   AND n."EntityType" = 0
                   AND n."EntityId" = f."FriendRequestId";

                -- Rows inserted by an earlier Up keep their IDs after a rollback/reapply.
                WITH legacy AS (
                    SELECT f.*, ROW_NUMBER() OVER (
                        PARTITION BY "RecipientUserId", "ActorUserId", "Type", "FriendRequestId"
                        ORDER BY "CreatedAtUtc", "Id") AS position
                    FROM "FriendNotifications" AS f
                    WHERE NOT EXISTS (
                        SELECT 1 FROM friend_notification_matches AS m WHERE m.legacy_id = f."Id")
                ), shared AS (
                    SELECT n.*, ROW_NUMBER() OVER (
                        PARTITION BY "RecipientUserId", "ActorUserId", "Type", "EntityId"
                        ORDER BY "CreatedAtUtc", "Id") AS position
                    FROM "Notifications" AS n
                    WHERE "EntityType" = 0 AND "Type" IN (0, 1)
                      AND NOT EXISTS (
                          SELECT 1 FROM friend_notification_matches AS m WHERE m.notification_id = n."Id")
                )
                INSERT INTO friend_notification_matches (legacy_id, notification_id)
                SELECT f."Id" AS legacy_id, n."Id" AS notification_id
                FROM legacy AS f
                INNER JOIN shared AS n
                    ON n."RecipientUserId" = f."RecipientUserId"
                   AND n."ActorUserId" = f."ActorUserId"
                   AND n."Type" = f."Type"
                   AND n."EntityId" = f."FriendRequestId"
                   AND n.position = f.position;

                UPDATE "Notifications" AS n
                SET "LegacyFriendNotificationId" = f."Id",
                    "IsRead" = n."IsRead" OR f."ReadAtUtc" IS NOT NULL,
                    "ReadAtUtc" = LEAST(n."ReadAtUtc", f."ReadAtUtc")
                FROM friend_notification_matches AS m
                INNER JOIN "FriendNotifications" AS f ON f."Id" = m.legacy_id
                WHERE n."Id" = m.notification_id;

                INSERT INTO "Notifications"
                    ("Id", "RecipientUserId", "ActorUserId", "Type", "EntityType", "EntityId",
                     "IsRead", "CreatedAtUtc", "ReadAtUtc", "LegacyFriendNotificationId")
                SELECT f."Id", f."RecipientUserId", f."ActorUserId", f."Type", 0, f."FriendRequestId",
                       f."ReadAtUtc" IS NOT NULL, f."CreatedAtUtc", f."ReadAtUtc", f."Id"
                FROM "FriendNotifications" AS f
                WHERE NOT EXISTS (
                    SELECT 1 FROM friend_notification_matches AS m WHERE m.legacy_id = f."Id"
                );
                """);

            migrationBuilder.DropTable(
                name: "FriendNotifications");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_LegacyFriendNotificationId",
                table: "Notifications",
                column: "LegacyFriendNotificationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_LegacyFriendNotificationId",
                table: "Notifications");

            migrationBuilder.CreateTable(
                name: "FriendNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FriendRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FriendNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FriendNotifications_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FriendNotifications_AspNetUsers_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FriendNotifications_FriendRequests_FriendRequestId",
                        column: x => x.FriendRequestId,
                        principalTable: "FriendRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FriendNotifications_ActorUserId",
                table: "FriendNotifications",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendNotifications_FriendRequestId",
                table: "FriendNotifications",
                column: "FriendRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendNotifications_RecipientUserId_ReadAtUtc_CreatedAtUtc",
                table: "FriendNotifications",
                columns: new[] { "RecipientUserId", "ReadAtUtc", "CreatedAtUtc" });

            migrationBuilder.Sql("""
                INSERT INTO "FriendNotifications"
                    ("Id", "RecipientUserId", "ActorUserId", "FriendRequestId", "Type", "CreatedAtUtc", "ReadAtUtc")
                SELECT COALESCE(n."LegacyFriendNotificationId", n."Id"), n."RecipientUserId", n."ActorUserId",
                       n."EntityId", n."Type", n."CreatedAtUtc",
                       CASE WHEN n."IsRead" THEN COALESCE(n."ReadAtUtc", n."CreatedAtUtc") END
                FROM "Notifications" AS n
                INNER JOIN "FriendRequests" AS r ON r."Id" = n."EntityId"
                WHERE n."EntityType" = 0 AND n."Type" IN (0, 1) AND n."ActorUserId" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "LegacyFriendNotificationId",
                table: "Notifications");
        }
    }
}
