using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UbuhlebethuConnectPro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Projects",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Projects");
        }
    }
}
