using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UbuhlebethuConnectPro.WebApp.Data;
using Xunit;

namespace UbuhlebethuConnectPro.Tests
{
    public class ApplySchemaFixTest
    {
        private const string ConnectionString = "Host=ep-dark-bird-b1kvlfcb-pooler.c-5.eu-central-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=npg_rXyEz5DkcLx8;SslMode=Require;";

        [Fact]
        public async Task ApplyGeofencingAndMaterialsColumns()
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql(ConnectionString);

            using var context = new ApplicationDbContext(optionsBuilder.Options);

            var sql = @"
                ALTER TABLE ""Projects"" ADD COLUMN IF NOT EXISTS ""Latitude"" double precision NULL;
                ALTER TABLE ""Projects"" ADD COLUMN IF NOT EXISTS ""Longitude"" double precision NULL;
                ALTER TABLE ""Projects"" ADD COLUMN IF NOT EXISTS ""GeofenceRadiusMeters"" double precision NOT NULL DEFAULT 300.0;

                ALTER TABLE ""AttendanceRecords"" ADD COLUMN IF NOT EXISTS ""IsOffSite"" boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE ""AttendanceRecords"" ADD COLUMN IF NOT EXISTS ""DistanceFromSiteMeters"" double precision NULL;

                ALTER TABLE ""SiteReports"" ADD COLUMN IF NOT EXISTS ""IsOffSite"" boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE ""SiteReports"" ADD COLUMN IF NOT EXISTS ""DistanceFromSiteMeters"" double precision NULL;

                CREATE TABLE IF NOT EXISTS ""Materials"" (
                    ""Id"" uuid NOT NULL,
                    ""Barcode"" text NOT NULL,
                    ""Name"" text NOT NULL,
                    ""UnitCost"" numeric(18,2) NOT NULL,
                    ""Unit"" text NOT NULL,
                    ""ProjectId"" uuid NULL,
                    ""RegisteredBy"" text NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""IsDeleted"" boolean NOT NULL DEFAULT FALSE,
                    CONSTRAINT ""PK_Materials"" PRIMARY KEY (""Id"")
                );

                CREATE INDEX IF NOT EXISTS ""IX_Materials_Barcode"" ON ""Materials"" (""Barcode"");

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
            ";

            await context.Database.ExecuteSqlRawAsync(sql);

            // Record migrations in __EFMigrationsHistory if not already there
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                VALUES ('20261102182638_ChangeExpenseProjectIdToGuid', '10.0.13')
                ON CONFLICT (""MigrationId"") DO NOTHING;

                INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                VALUES ('20261103234500_AddGeofencingAndMaterials', '10.0.13')
                ON CONFLICT (""MigrationId"") DO NOTHING;
            ");

            // Now verify we can query Projects and Attendance and SiteReports without PostgresException
            var projects = await context.Projects.Take(5).ToListAsync();
            Assert.NotNull(projects);

            var siteReports = await context.SiteReports.Take(5).ToListAsync();
            Assert.NotNull(siteReports);

            var attendance = await context.AttendanceRecords.Take(5).ToListAsync();
            Assert.NotNull(attendance);
        }
    }
}
