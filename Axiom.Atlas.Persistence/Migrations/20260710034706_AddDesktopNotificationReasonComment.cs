using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axiom.Atlas.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDesktopNotificationReasonComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReasonComment",
                table: "DesktopNotifications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReasonComment",
                table: "DesktopNotifications");
        }
    }
}
