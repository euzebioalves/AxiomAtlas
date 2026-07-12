using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axiom.Atlas.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGlpiTicketWorkspaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GlpiTicketWorkspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GlpiTicketId = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EntityPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClientEntityName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Classification = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TicketPayloadJson = table.Column<string>(type: "text", nullable: false),
                    FollowUpsJson = table.Column<string>(type: "text", nullable: false),
                    AttachmentsJson = table.Column<string>(type: "text", nullable: false),
                    RequirementMarkdown = table.Column<string>(type: "text", nullable: true),
                    OpenProjectWorkPackageId = table.Column<int>(type: "integer", nullable: true),
                    OpenProjectWorkPackageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GlpiDevOpsFieldId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GlpiDevOpsUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlpiTicketWorkspaces", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GlpiTicketWorkspaces_GlpiTicketId",
                table: "GlpiTicketWorkspaces",
                column: "GlpiTicketId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GlpiTicketWorkspaces");
        }
    }
}
