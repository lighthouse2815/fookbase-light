using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Code.Persistence.Migrations;

[DbContext(typeof(FookbaseDbContext))]
[Migration("20260916065000_AddPinnedProfilePosts")]
public partial class AddPinnedProfilePosts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsPinned",
            table: "Posts",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_Posts_AuthorUserId_IsPinned_CreatedAtUtc_Id",
            table: "Posts",
            columns: new[] { "AuthorUserId", "IsPinned", "CreatedAtUtc", "Id" },
            filter: "\"DeletedAtUtc\" IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Posts_AuthorUserId_IsPinned_CreatedAtUtc_Id",
            table: "Posts");

        migrationBuilder.DropColumn(
            name: "IsPinned",
            table: "Posts");
    }
}
