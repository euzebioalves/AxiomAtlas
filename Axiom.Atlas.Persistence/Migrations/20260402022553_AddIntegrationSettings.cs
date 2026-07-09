using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axiom.Atlas.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Integrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    Environment = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    BaseUrl = table.Column<string>(type: "text", nullable: false),
                    PrimaryToken = table.Column<string>(type: "text", nullable: false),
                    SecondaryToken = table.Column<string>(type: "text", nullable: false),
                    AdditionalSettings = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Integrations", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Integrations");
        }
    }
}
