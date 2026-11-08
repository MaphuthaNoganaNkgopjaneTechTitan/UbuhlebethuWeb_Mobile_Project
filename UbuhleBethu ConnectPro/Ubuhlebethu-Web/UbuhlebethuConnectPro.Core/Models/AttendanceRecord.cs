using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UbuhlebethuConnectPro.Core.Models
{
    public class AttendanceRecord
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }
        
        [Required]
        public Guid ProjectId { get; set; }
        
        [ForeignKey("ProjectId")]
        public Project? Project { get; set; }
        
        public DateTime CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        
        public double? CheckInLatitude { get; set; }
        public double? CheckInLongitude { get; set; }
        
        public bool IsApproved { get; set; }
        public bool IsOffSite { get; set; }
        public double? DistanceFromSiteMeters { get; set; }
    }
}
