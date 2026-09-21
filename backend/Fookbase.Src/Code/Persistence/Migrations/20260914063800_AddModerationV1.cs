using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations;

[DbContext(typeof(FookbaseDbContext))]
[Migration("20260914063800_AddModerationV1")]
public partial class AddModerationV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ResolvedAtUtc", table: "ContentReports", type: "timestamp with time zone", nullable: true);

        migrationBuilder.CreateTable(
            name: "ModerationActions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ReportId = table.Column<Guid>(type: "uuid", nullable: true),
                ModeratorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                SubjectUserId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetType = table.Column<int>(type: "integer", nullable: false),
                TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                ActionType = table.Column<int>(type: "integer", nullable: false),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                InternalNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_ModerationActions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "UserModerationStates",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                WarningCount = table.Column<int>(type: "integer", nullable: false),
                SuspendedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                DisabledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_UserModerationStates", x => x.UserId));

        migrationBuilder.CreateIndex(name: "IX_ContentReports_Status_CreatedAtUtc_Id", table: "ContentReports", columns: new[] { "Status", "CreatedAtUtc", "Id" });
        migrationBuilder.CreateIndex(name: "IX_ModerationActions_TargetType_TargetId_CreatedAtUtc", table: "ModerationActions", columns: new[] { "TargetType", "TargetId", "CreatedAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_ModerationActions_SubjectUserId_CreatedAtUtc", table: "ModerationActions", columns: new[] { "SubjectUserId", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ModerationActions");
        migrationBuilder.DropTable(name: "UserModerationStates");
        migrationBuilder.DropIndex(name: "IX_ContentReports_Status_CreatedAtUtc_Id", table: "ContentReports");
        migrationBuilder.DropColumn(name: "ResolvedAtUtc", table: "ContentReports");
    }
}
