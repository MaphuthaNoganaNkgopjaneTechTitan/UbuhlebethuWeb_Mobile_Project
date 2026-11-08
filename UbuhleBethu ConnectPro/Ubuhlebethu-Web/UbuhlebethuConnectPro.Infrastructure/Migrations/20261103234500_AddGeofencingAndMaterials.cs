using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UbuhlebethuConnectPro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGeofencingAndMaterials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Projects",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Projects",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GeofenceRadiusMeters",
                table: "Projects",
                type: "double precision",
                nullable: false,
                defaultValue: 300.0);

            migrationBuilder.AddColumn<bool>(
                name: "IsOffSite",
                table: "AttendanceRecords",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "DistanceFromSiteMeters",
                table: "AttendanceRecords",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOffSite",
                table: "SiteReports",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "DistanceFromSiteMeters",
                table: "SiteReports",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Barcode = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    RegisteredBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Materials_Barcode",
                table: "Materials",
                column: "Barcode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "GeofenceRadiusMeters",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IsOffSite",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "DistanceFromSiteMeters",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "IsOffSite",
                table: "SiteReports");

            migrationBuilder.DropColumn(
                name: "DistanceFromSiteMeters",
                table: "SiteReports");
        }
    }
}
