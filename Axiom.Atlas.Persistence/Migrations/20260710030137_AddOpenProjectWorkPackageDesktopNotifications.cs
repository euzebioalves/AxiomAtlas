using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Axiom.Atlas.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenProjectWorkPackageDesktopNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DesktopNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkPackageId = table.Column<int>(type: "integer", nullable: false),
                    WorkPackageSubject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StatusName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PreviousStatusName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WorkPackageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesktopNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenProjectWorkPackageStatusSnapshots",
                columns: table => new
                {
                    WorkPackageId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StatusName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenProjectWorkPackageStatusSnapshots", x => x.WorkPackageId);
                });

            migrationBuilder.CreateTable(
                name: "UserDesktopNotificationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDesktopNotificationSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DesktopNotifications_UserId_DeliveredAt",
                table: "DesktopNotifications",
                columns: new[] { "UserId", "DeliveredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserDesktopNotificationSettings_UserId",
                table: "UserDesktopNotificationSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DesktopNotifications");

            migrationBuilder.DropTable(
                name: "OpenProjectWorkPackageStatusSnapshots");

            migrationBuilder.DropTable(
                name: "UserDesktopNotificationSettings");
        }
    }
}
