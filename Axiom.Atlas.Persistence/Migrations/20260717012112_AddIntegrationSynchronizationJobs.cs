using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axiom.Atlas.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationSynchronizationJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationSynchronizationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CorrelationKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: true),
                    GlpiTicketId = table.Column<long>(type: "bigint", nullable: true),
                    OpenProjectWorkPackageId = table.Column<int>(type: "integer", nullable: true),
                    RequestedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    AvailableAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationSynchronizationJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationSynchronizationJobs_Status_AvailableAt",
                table: "IntegrationSynchronizationJobs",
                columns: new[] { "Status", "AvailableAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationSynchronizationJobs_Type_CorrelationKey_CreatedAt",
                table: "IntegrationSynchronizationJobs",
                columns: new[] { "Type", "CorrelationKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationSynchronizationJobs_WorkspaceId",
                table: "IntegrationSynchronizationJobs",
                column: "WorkspaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationSynchronizationJobs");
        }
    }
}
