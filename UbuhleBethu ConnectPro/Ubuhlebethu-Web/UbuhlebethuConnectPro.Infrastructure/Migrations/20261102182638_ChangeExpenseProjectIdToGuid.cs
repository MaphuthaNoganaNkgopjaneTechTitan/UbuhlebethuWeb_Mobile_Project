using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UbuhlebethuConnectPro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeExpenseProjectIdToGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""SupportTickets"" DROP COLUMN IF EXISTS ""UpdatedAt"";

                ALTER TABLE ""Payments"" ADD COLUMN IF NOT EXISTS ""PaymentMethod"" integer NOT NULL DEFAULT 0;
                ALTER TABLE ""Payments"" ADD COLUMN IF NOT EXISTS ""PaymentStatus"" integer NOT NULL DEFAULT 0;
                ALTER TABLE ""Payments"" ADD COLUMN IF NOT EXISTS ""ProofPath"" text;
                ALTER TABLE ""Payments"" ADD COLUMN IF NOT EXISTS ""RecordedBy"" text;
                ALTER TABLE ""Payments"" ADD COLUMN IF NOT EXISTS ""VerifiedAt"" timestamp with time zone;
                ALTER TABLE ""Payments"" ADD COLUMN IF NOT EXISTS ""VerifiedBy"" text;

                ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""DueDate"" timestamp with time zone NOT NULL DEFAULT '0001-01-01 00:00:00+00';
                ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""IssueDate"" timestamp with time zone NOT NULL DEFAULT '0001-01-01 00:00:00+00';
                ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""PaidAmount"" numeric(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""Status"" text NOT NULL DEFAULT '';

                CREATE TABLE IF NOT EXISTS ""ExpenseRecords"" (
                    ""Id"" uuid NOT NULL,
                    ""ManagerName"" text NOT NULL,
                    ""ItemDescription"" text NOT NULL,
                    ""Amount"" numeric(18,2) NOT NULL,
                    ""Status"" text NOT NULL,
                    ""ProjectId"" uuid NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_ExpenseRecords"" PRIMARY KEY (""Id"")
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpenseRecords");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProofPath",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RecordedBy",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VerifiedBy",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "IssueDate",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Invoices");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "SupportTickets",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
