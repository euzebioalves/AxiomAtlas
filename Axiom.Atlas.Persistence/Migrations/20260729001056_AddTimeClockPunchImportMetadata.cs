using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axiom.Atlas.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeClockPunchImportMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalRecordId",
                table: "TimeClockPunches",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalUserId",
                table: "TimeClockPunches",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImportBatchId",
                table: "TimeClockPunches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportFileHash",
                table: "TimeClockPunches",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportFileName",
                table: "TimeClockPunches",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ImportedAt",
                table: "TimeClockPunches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceCreatedAt",
                table: "TimeClockPunches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceUpdatedAt",
                table: "TimeClockPunches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeClockPunches_UserId_ExternalRecordId",
                table: "TimeClockPunches",
                columns: new[] { "UserId", "ExternalRecordId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TimeClockPunches_UserId_ExternalRecordId",
                table: "TimeClockPunches");

            migrationBuilder.DropColumn(
                name: "ExternalRecordId",
                table: "TimeClockPunches");

            migrationBuilder.DropColumn(
                name: "ExternalUserId",
                table: "TimeClockPunches");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "TimeClockPunches");

            migrationBuilder.DropColumn(
                name: "ImportFileHash",
                table: "TimeClockPunches");

            migrationBuilder.DropColumn(
                name: "ImportFileName",
                table: "TimeClockPunches");

            migrationBuilder.DropColumn(
                name: "ImportedAt",
                table: "TimeClockPunches");

            migrationBuilder.DropColumn(
                name: "SourceCreatedAt",
                table: "TimeClockPunches");

            migrationBuilder.DropColumn(
                name: "SourceUpdatedAt",
                table: "TimeClockPunches");
        }
    }
}
