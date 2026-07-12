using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Axiom.Atlas.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGlpiImprovementTicketProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GlpiImprovementTickets",
                columns: table => new
                {
                    GlpiTicketId = table.Column<long>(type: "bigint", nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    GlpiTicketUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    StatusName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EntityPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClientEntityName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    WorkPackageId = table.Column<int>(type: "integer", nullable: true),
                    WorkPackageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    WorkPackageStatus = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WorkPackageCreator = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    WorkPackageCreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsInImprovementQueue = table.Column<bool>(type: "boolean", nullable: false),
                    LastSynchronizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlpiImprovementTickets", x => x.GlpiTicketId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GlpiImprovementTickets_IsInImprovementQueue_StatusCode_Open~",
                table: "GlpiImprovementTickets",
                columns: new[] { "IsInImprovementQueue", "StatusCode", "OpenedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GlpiImprovementTickets");
        }
    }
}
