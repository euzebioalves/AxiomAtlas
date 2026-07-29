using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axiom.Atlas.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeClockAbsenceImportMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalRecordId",
                table: "TimeClockUnjustifiedAbsences",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalUserId",
                table: "TimeClockUnjustifiedAbsences",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImportBatchId",
                table: "TimeClockUnjustifiedAbsences",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportFileHash",
                table: "TimeClockUnjustifiedAbsences",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportFileName",
                table: "TimeClockUnjustifiedAbsences",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ImportedAt",
                table: "TimeClockUnjustifiedAbsences",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceCreatedAt",
                table: "TimeClockUnjustifiedAbsences",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceUpdatedAt",
                table: "TimeClockUnjustifiedAbsences",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalRecordId",
                table: "TimeClockAbsences",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalUserId",
                table: "TimeClockAbsences",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImportBatchId",
                table: "TimeClockAbsences",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportFileHash",
                table: "TimeClockAbsences",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportFileName",
                table: "TimeClockAbsences",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ImportedAt",
                table: "TimeClockAbsences",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceCreatedAt",
                table: "TimeClockAbsences",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceUpdatedAt",
                table: "TimeClockAbsences",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeClockUnjustifiedAbsences_UserId_ExternalRecordId",
                table: "TimeClockUnjustifiedAbsences",
                columns: new[] { "UserId", "ExternalRecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeClockAbsences_UserId_ExternalRecordId",
                table: "TimeClockAbsences",
                columns: new[] { "UserId", "ExternalRecordId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TimeClockUnjustifiedAbsences_UserId_ExternalRecordId",
                table: "TimeClockUnjustifiedAbsences");

            migrationBuilder.DropIndex(
                name: "IX_TimeClockAbsences_UserId_ExternalRecordId",
                table: "TimeClockAbsences");

            migrationBuilder.DropColumn(
                name: "ExternalRecordId",
                table: "TimeClockUnjustifiedAbsences");

            migrationBuilder.DropColumn(
                name: "ExternalUserId",
                table: "TimeClockUnjustifiedAbsences");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "TimeClockUnjustifiedAbsences");

            migrationBuilder.DropColumn(
                name: "ImportFileHash",
                table: "TimeClockUnjustifiedAbsences");

            migrationBuilder.DropColumn(
                name: "ImportFileName",
                table: "TimeClockUnjustifiedAbsences");

            migrationBuilder.DropColumn(
                name: "ImportedAt",
                table: "TimeClockUnjustifiedAbsences");

            migrationBuilder.DropColumn(
                name: "SourceCreatedAt",
                table: "TimeClockUnjustifiedAbsences");

            migrationBuilder.DropColumn(
                name: "SourceUpdatedAt",
                table: "TimeClockUnjustifiedAbsences");

            migrationBuilder.DropColumn(
                name: "ExternalRecordId",
                table: "TimeClockAbsences");

            migrationBuilder.DropColumn(
                name: "ExternalUserId",
                table: "TimeClockAbsences");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "TimeClockAbsences");

            migrationBuilder.DropColumn(
                name: "ImportFileHash",
                table: "TimeClockAbsences");

            migrationBuilder.DropColumn(
                name: "ImportFileName",
                table: "TimeClockAbsences");

            migrationBuilder.DropColumn(
                name: "ImportedAt",
                table: "TimeClockAbsences");

            migrationBuilder.DropColumn(
                name: "SourceCreatedAt",
                table: "TimeClockAbsences");

            migrationBuilder.DropColumn(
                name: "SourceUpdatedAt",
                table: "TimeClockAbsences");
        }
    }
}
