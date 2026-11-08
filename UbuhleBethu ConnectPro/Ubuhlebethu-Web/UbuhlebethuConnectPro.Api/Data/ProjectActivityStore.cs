using Microsoft.EntityFrameworkCore;

namespace UbuhlebethuConnectPro.Api.Data
{
    /// <summary>One saved item on a project: a daily log, milestone, purchase order, document or client contact record.</summary>
    public class ProjectActivityRow
    {
        public string Id { get; set; } = "";
        public string ProjectId { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Body { get; set; }
        public string? Amount { get; set; }
        public string? DueDate { get; set; }
        public string? Status { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public string? CreatedBy { get; set; }
        public string CreatedAt { get; set; } = "";
    }

    public class StringValue { public string? Value { get; set; } }

    /// <summary>
    /// Stores project activities with plain SQL in its own table, so no EF migration is needed.
    /// The DDL uses only types that PostgreSQL and SQLite both accept.
    /// </summary>
    public static class ProjectActivityStore
    {
        public static readonly string[] Kinds = { "DailyLog", "Milestone", "PurchaseOrder", "Document", "ClientContact" };
        public const int MaxFileChars = 2_800_000; // about 2 MB once base64-encoded

        private const string Cols = "\"Id\",\"ProjectId\",\"Kind\",\"Title\",\"Body\",\"Amount\",\"DueDate\",\"Status\",\"FileName\",\"ContentType\",\"CreatedBy\",\"CreatedAt\"";

        public static async Task EnsureTableAsync(DbContext db)
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS \"ProjectActivities\" (" +
                "\"Id\" TEXT PRIMARY KEY, \"ProjectId\" TEXT NOT NULL, \"Kind\" TEXT NOT NULL, \"Title\" TEXT NOT NULL, " +
                "\"Body\" TEXT NULL, \"Amount\" TEXT NULL, \"DueDate\" TEXT NULL, \"Status\" TEXT NULL, " +
                "\"FileName\" TEXT NULL, \"ContentType\" TEXT NULL, \"Content\" TEXT NULL, \"CreatedBy\" TEXT NULL, \"CreatedAt\" TEXT NOT NULL)");
            await db.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IF NOT EXISTS \"IX_ProjectActivities_Project\" ON \"ProjectActivities\" (\"ProjectId\", \"Kind\")");

            try
            {
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE \"Projects\" ADD COLUMN IF NOT EXISTS \"Latitude\" double precision NULL; " +
                    "ALTER TABLE \"Projects\" ADD COLUMN IF NOT EXISTS \"Longitude\" double precision NULL; " +
                    "ALTER TABLE \"Projects\" ADD COLUMN IF NOT EXISTS \"GeofenceRadiusMeters\" double precision NOT NULL DEFAULT 300.0; " +
                    "ALTER TABLE \"SiteReports\" ADD COLUMN IF NOT EXISTS \"IsOffSite\" boolean NOT NULL DEFAULT false; " +
                    "ALTER TABLE \"SiteReports\" ADD COLUMN IF NOT EXISTS \"DistanceFromSiteMeters\" double precision NULL; " +
                    "ALTER TABLE \"AttendanceRecords\" ADD COLUMN IF NOT EXISTS \"CheckInLatitude\" double precision NULL; " +
                    "ALTER TABLE \"AttendanceRecords\" ADD COLUMN IF NOT EXISTS \"CheckInLongitude\" double precision NULL; " +
                    "ALTER TABLE \"AttendanceRecords\" ADD COLUMN IF NOT EXISTS \"IsApproved\" boolean NOT NULL DEFAULT false; " +
                    "ALTER TABLE \"AttendanceRecords\" ADD COLUMN IF NOT EXISTS \"IsOffSite\" boolean NOT NULL DEFAULT false; " +
                    "ALTER TABLE \"AttendanceRecords\" ADD COLUMN IF NOT EXISTS \"DistanceFromSiteMeters\" double precision NULL;");
            }
            catch
            {
                // Non-fatal if database provider does not support Postgres ALTER TABLE syntax in test harness
            }
        }

        public static Task<List<ProjectActivityRow>> ListAsync(DbContext db, Guid projectId, CancellationToken ct = default)
        {
            var pid = projectId.ToString();
            return db.Database.SqlQuery<ProjectActivityRow>(
                    $"SELECT \"Id\",\"ProjectId\",\"Kind\",\"Title\",\"Body\",\"Amount\",\"DueDate\",\"Status\",\"FileName\",\"ContentType\",\"CreatedBy\",\"CreatedAt\" FROM \"ProjectActivities\" WHERE \"ProjectId\" = {pid} ORDER BY \"CreatedAt\" DESC")
                .ToListAsync(ct);
        }

        public static async Task<ProjectActivityRow> AddAsync(DbContext db, Guid projectId, string kind, string title, string? body,
            string? amount, string? dueDate, string? status, string? fileName, string? contentType, string? content, string? createdBy)
        {
            var row = new ProjectActivityRow
            {
                Id = Guid.NewGuid().ToString(), ProjectId = projectId.ToString(), Kind = kind, Title = title, Body = body,
                Amount = amount, DueDate = dueDate, Status = status, FileName = fileName, ContentType = contentType,
                CreatedBy = createdBy, CreatedAt = DateTime.UtcNow.ToString("o")
            };
            await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO ""ProjectActivities""
                (""Id"",""ProjectId"",""Kind"",""Title"",""Body"",""Amount"",""DueDate"",""Status"",""FileName"",""ContentType"",""Content"",""CreatedBy"",""CreatedAt"")
                VALUES ({row.Id},{row.ProjectId},{row.Kind},{row.Title},{row.Body},{row.Amount},{row.DueDate},{row.Status},{row.FileName},{row.ContentType},{content},{row.CreatedBy},{row.CreatedAt})");
            return row;
        }

        public static async Task<bool> UpdateStatusAsync(DbContext db, Guid projectId, string activityId, string kind, string status)
        {
            var pid = projectId.ToString();
            var n = await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"ProjectActivities\" SET \"Status\" = {status} WHERE \"Id\" = {activityId} AND \"ProjectId\" = {pid} AND \"Kind\" = {kind}");
            return n > 0;
        }

        public static async Task<(ProjectActivityRow? Row, string? Content)> GetFileAsync(DbContext db, string id, CancellationToken ct = default)
        {
            var row = (await db.Database.SqlQuery<ProjectActivityRow>(
                $"SELECT \"Id\",\"ProjectId\",\"Kind\",\"Title\",\"Body\",\"Amount\",\"DueDate\",\"Status\",\"FileName\",\"ContentType\",\"CreatedBy\",\"CreatedAt\" FROM \"ProjectActivities\" WHERE \"Id\" = {id} AND \"Kind\" = 'Document'")
                .ToListAsync(ct)).FirstOrDefault();
            if (row == null) return (null, null);
            var content = (await db.Database.SqlQuery<StringValue>(
                $"SELECT \"Content\" AS \"Value\" FROM \"ProjectActivities\" WHERE \"Id\" = {id}").ToListAsync(ct)).FirstOrDefault()?.Value;
            return (row, content);
        }
    }
}
