using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Code.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaDeletionRetryMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ObjectDeletions_ProcessedAtUtc_CreatedAtUtc",
                table: "ObjectDeletions");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FailedAtUtc",
                table: "ObjectDeletions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAtUtc",
                table: "ObjectDeletions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_ObjectDeletions_ProcessedAtUtc_FailedAtUtc_NextAttemptAtUtc~",
                table: "ObjectDeletions",
                columns: new[] { "ProcessedAtUtc", "FailedAtUtc", "NextAttemptAtUtc", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ObjectDeletions_ProcessedAtUtc_FailedAtUtc_NextAttemptAtUtc~",
                table: "ObjectDeletions");

            migrationBuilder.DropColumn(
                name: "FailedAtUtc",
                table: "ObjectDeletions");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                table: "ObjectDeletions");

            migrationBuilder.CreateIndex(
                name: "IX_ObjectDeletions_ProcessedAtUtc_CreatedAtUtc",
                table: "ObjectDeletions",
                columns: new[] { "ProcessedAtUtc", "CreatedAtUtc" });
        }
    }
}
