using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace UbuhlebethuConnectPro.Core.Models
{
    public class SupportTicket
    {
        // ── Physical Columns (Present in PostgreSQL DB) ──
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Open";
        public string? Response { get; set; }

        // ── In-Memory / Unmapped Fields (NOT in PostgreSQL DB) ──
        [NotMapped]
        public string Message
        {
            get => Description;
            set => Description = value;
        }

        [NotMapped]
        public string CreatedByName
        {
            get => string.IsNullOrEmpty(CreatedBy) ? "Client" : CreatedBy;
            set => CreatedBy = value;
        }

        [NotMapped]
        public string Category { get; set; } = "General";

        [NotMapped]
        public string AssignedRole { get; set; } = "Manager";

        [NotMapped]
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public string? RespondedBy { get; set; }

        [NotMapped]
        public DateTime? RespondedAt { get; set; }
    }
}