using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Code.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPostTextBackground : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TextBackground",
                table: "Posts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TextBackground",
                table: "Posts");
        }
    }
}
