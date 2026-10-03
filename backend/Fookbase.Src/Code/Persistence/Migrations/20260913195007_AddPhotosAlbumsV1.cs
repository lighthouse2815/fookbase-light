using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotosAlbumsV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlbumMedia",
                columns: table => new
                {
                    AlbumId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Caption = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<long>(type: "bigint", nullable: false),
                    AddedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbumMedia", x => new { x.AlbumId, x.MediaId });
                });

            migrationBuilder.CreateTable(
                name: "PhotoAlbums",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Privacy = table.Column<int>(type: "integer", nullable: false),
                    AlbumType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoAlbums", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlbumMedia_AlbumId_SortOrder_MediaId",
                table: "AlbumMedia",
                columns: new[] { "AlbumId", "SortOrder", "MediaId" });

            migrationBuilder.CreateIndex(
                name: "IX_AlbumMedia_MediaId",
                table: "AlbumMedia",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoAlbums_OwnerUserId_AlbumType",
                table: "PhotoAlbums",
                columns: new[] { "OwnerUserId", "AlbumType" },
                unique: true,
                filter: "\"DeletedAtUtc\" IS NULL AND \"AlbumType\" <> 0");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoAlbums_OwnerUserId_CreatedAtUtc_Id",
                table: "PhotoAlbums",
                columns: new[] { "OwnerUserId", "CreatedAtUtc", "Id" },
                filter: "\"DeletedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlbumMedia");

            migrationBuilder.DropTable(
                name: "PhotoAlbums");
        }
    }
}
