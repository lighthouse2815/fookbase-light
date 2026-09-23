using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Code.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddZolaPushNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PushDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpoPushToken = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    RegisteredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DisabledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PushDeliveryReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PushDeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpoReceiptId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CheckedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CheckAttempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushDeliveryReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PushDeliveryReceipts_PushDevices_PushDeviceId",
                        column: x => x.PushDeviceId,
                        principalTable: "PushDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PushDeliveryReceipts_CheckedAtUtc_AvailableAtUtc",
                table: "PushDeliveryReceipts",
                columns: new[] { "CheckedAtUtc", "AvailableAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PushDeliveryReceipts_ExpoReceiptId",
                table: "PushDeliveryReceipts",
                column: "ExpoReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushDeliveryReceipts_PushDeviceId",
                table: "PushDeliveryReceipts",
                column: "PushDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_PushDevices_ExpoPushToken",
                table: "PushDevices",
                column: "ExpoPushToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushDevices_UserId_DisabledAtUtc",
                table: "PushDevices",
                columns: new[] { "UserId", "DisabledAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PushDeliveryReceipts");

            migrationBuilder.DropTable(
                name: "PushDevices");
        }
    }
}
