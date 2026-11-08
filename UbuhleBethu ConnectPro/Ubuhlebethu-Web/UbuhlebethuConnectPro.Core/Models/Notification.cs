using System;

namespace UbuhlebethuConnectPro.Core.Models
{
    public class Notification
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public bool IsDeleted { get; set; } = false;
        public string UserId { get; set; } = string.Empty; // Identity username
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
        public string? ReferenceId { get; set; }
    }
}

