using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axiom.Atlas.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGlpiTicketWorkspaceImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GlpiTicketWorkspaceImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlpiTicketWorkspaceImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlpiTicketWorkspaceImages_GlpiTicketWorkspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "GlpiTicketWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GlpiTicketWorkspaceImages_WorkspaceId",
                table: "GlpiTicketWorkspaceImages",
                column: "WorkspaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GlpiTicketWorkspaceImages");
        }
    }
}
