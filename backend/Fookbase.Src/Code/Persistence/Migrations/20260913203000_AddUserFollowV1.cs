using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations;

[DbContext(typeof(FookbaseDbContext))]
[Migration("20260913203000_AddUserFollowV1")]
public partial class AddUserFollowV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UserFollows",
            columns: table => new
            {
                FollowerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                FollowingUserId = table.Column<Guid>(type: "uuid", nullable: false),
                FollowedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserFollows", x => new { x.FollowerUserId, x.FollowingUserId });
                table.CheckConstraint(
                    "CK_UserFollows_DifferentUsers",
                    "\"FollowerUserId\" <> \"FollowingUserId\"");
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserFollows_FollowerUserId_FollowedAtUtc_FollowingUserId",
            table: "UserFollows",
            columns: new[] { "FollowerUserId", "FollowedAtUtc", "FollowingUserId" });

        migrationBuilder.CreateIndex(
            name: "IX_UserFollows_FollowingUserId_FollowerUserId",
            table: "UserFollows",
            columns: new[] { "FollowingUserId", "FollowerUserId" });

        migrationBuilder.CreateIndex(
            name: "IX_UserFollows_FollowingUserId_FollowedAtUtc_FollowerUserId",
            table: "UserFollows",
            columns: new[] { "FollowingUserId", "FollowedAtUtc", "FollowerUserId" });

        migrationBuilder.Sql("""
            INSERT INTO "UserFollows" ("FollowerUserId", "FollowingUserId", "FollowedAtUtc")
            SELECT "UserId1", "UserId2", "CreatedAtUtc"
            FROM "Friendships"
            ON CONFLICT DO NOTHING;

            INSERT INTO "UserFollows" ("FollowerUserId", "FollowingUserId", "FollowedAtUtc")
            SELECT "UserId2", "UserId1", "CreatedAtUtc"
            FROM "Friendships"
            ON CONFLICT DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "UserFollows");
    }
}
