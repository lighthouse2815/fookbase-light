using System;
using Fookbase.Api.Modules.Media.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Modules.Media.Data.Migrations;

[DbContext(typeof(MediaDbContext))]
[Migration("20260910110000_AddProfileMediaReferences")]
public partial class AddProfileMediaReferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProfileMediaReferences",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Slot = table.Column<int>(type: "integer", nullable: false),
                MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                AttachedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProfileMediaReferences", x => new { x.UserId, x.Slot });
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProfileMediaReferences_MediaId",
            table: "ProfileMediaReferences",
            column: "MediaId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ProfileMediaReferences");
    }
}
