using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axiom.Atlas.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeClockModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GlobalTimeClockSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToleranceMinutes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalTimeClockSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeClockAbsences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PeriodType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Observation = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeClockAbsences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeClockPunches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PunchDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PunchTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Nsr = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    Observation = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeClockPunches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeClockUnjustifiedAbsences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AbsenceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Observation = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeClockUnjustifiedAbsences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserWorkScheduleSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntryTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    ExitTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    LunchIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWorkScheduleSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeClockAbsenceAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AbsenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeClockAbsenceAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeClockAbsenceAttachments_TimeClockAbsences_AbsenceId",
                        column: x => x.AbsenceId,
                        principalTable: "TimeClockAbsences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimeClockAbsenceAttachments_AbsenceId",
                table: "TimeClockAbsenceAttachments",
                column: "AbsenceId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeClockPunches_UserId_PunchDate_Type",
                table: "TimeClockPunches",
                columns: new[] { "UserId", "PunchDate", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeClockUnjustifiedAbsences_UserId_AbsenceDate",
                table: "TimeClockUnjustifiedAbsences",
                columns: new[] { "UserId", "AbsenceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWorkScheduleSettings_UserId",
                table: "UserWorkScheduleSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GlobalTimeClockSettings");

            migrationBuilder.DropTable(
                name: "TimeClockAbsenceAttachments");

            migrationBuilder.DropTable(
                name: "TimeClockPunches");

            migrationBuilder.DropTable(
                name: "TimeClockUnjustifiedAbsences");

            migrationBuilder.DropTable(
                name: "UserWorkScheduleSettings");

            migrationBuilder.DropTable(
                name: "TimeClockAbsences");
        }
    }
}
