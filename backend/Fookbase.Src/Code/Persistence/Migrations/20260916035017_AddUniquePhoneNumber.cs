using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Code.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniquePhoneNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "PhoneNumberIndex",
                table: "AspNetUsers",
                column: "PhoneNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "PhoneNumberIndex",
                table: "AspNetUsers");
        }
    }
}
