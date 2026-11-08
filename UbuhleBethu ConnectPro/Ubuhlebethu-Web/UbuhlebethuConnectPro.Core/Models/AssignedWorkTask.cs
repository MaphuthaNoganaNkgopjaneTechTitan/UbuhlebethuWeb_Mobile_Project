using System;

namespace UbuhlebethuConnectPro.WebApp.Models
{
    public class AssignedWorkTask
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ProjectId { get; set; } = string.Empty;
        public string TaskTitle { get; set; } = string.Empty;
        public string AssignedWorker { get; set; } = string.Empty;
        public bool IsCompleted { get; set; } = false;
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}
