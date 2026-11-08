using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UbuhlebethuConnectPro.Core.Services.AI;
using UbuhlebethuConnectPro.WebApp.Data;

namespace UbuhlebethuConnectPro.Api.Data
{
    /// <summary>
    /// Durable per-user assistant memory. Uses plain parameterised SQL in its own table (same approach as
    /// <see cref="ProjectActivityStore"/>) so no EF migration is required. Only the user's key and a JSON blob of
    /// preferences/facts are stored - never chat transcripts.
    /// </summary>
    public class DbUserAIMemoryStore : IUserAIMemoryStore
    {
        private readonly IServiceScopeFactory _scopes;
        public DbUserAIMemoryStore(IServiceScopeFactory scopes) => _scopes = scopes;

        public static async Task EnsureTableAsync(DbContext db)
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS \"AiUserMemory\" (\"UserKey\" TEXT PRIMARY KEY, \"Data\" TEXT NOT NULL, \"UpdatedAt\" TEXT NOT NULL)");
        }

        public async Task<UserMemoryContext?> LoadAsync(string userKey)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var json = await db.Database
                .SqlQuery<string>($"SELECT \"Data\" AS \"Value\" FROM \"AiUserMemory\" WHERE \"UserKey\" = {userKey}")
                .FirstOrDefaultAsync();
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<UserMemoryContext>(json);
        }

        public async Task SaveAsync(UserMemoryContext memory)
        {
            string json;
            lock (memory) { json = JsonSerializer.Serialize(memory); }
            var key = memory.UserName;
            var now = DateTime.UtcNow.ToString("O");

            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO \"AiUserMemory\" (\"UserKey\", \"Data\", \"UpdatedAt\") VALUES ({key}, {json}, {now}) ON CONFLICT (\"UserKey\") DO UPDATE SET \"Data\" = EXCLUDED.\"Data\", \"UpdatedAt\" = EXCLUDED.\"UpdatedAt\"");
        }

        public async Task DeleteAsync(string userKey)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlAsync($"DELETE FROM \"AiUserMemory\" WHERE \"UserKey\" = {userKey}");
        }
    }
}
