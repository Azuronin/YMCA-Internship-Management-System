using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        public int? UserId { get; set; }
        [ForeignKey("UserId")]
        public User User { get; set; }

        public int? SenderId { get; set; }
        [ForeignKey("SenderId")]
        public User Sender { get; set; }
        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Message { get; set; }

        public string NotificationType { get; set; } // "Attendance", "Document", "Application", etc.

        public int? RelatedId { get; set; } // ID of related entity (ApplicantId, AttendanceId, etc.)

        public string RelatedEntity { get; set; } // Name of related entity ("Applicant", "Attendance", etc.)

        public string Link { get; set; } // Redirect to any page e.g., "ADMIN/Attendance"

        public bool IsRead { get; set; } = false;
        public bool IsDeleted { get; set; } 
        public DateTime? DeletedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}