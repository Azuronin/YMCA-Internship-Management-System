using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class Attendance
    {
        [Key]
        public int AttendanceId { get; set; }

        public int UserId { get; set; }
        public User? Intern { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [DataType(DataType.Time)]
        public DateTime? TimeIn { get; set; }

        [DataType(DataType.Time)]
        public DateTime? TimeOut { get; set; }

        // Removed Opening/Closing times from here
        public string? ProofImagePath { get; set; }
        public int? IsLate { get; set; }
        public bool IsAbsent { get; set; }
        public bool IsDeleted { get; set; } = false;
        public string? Remarks { get; set; }
        public decimal? RenderedHours { get; set; }
        public string? ApprovalStatus { get; set; } = "Pending";
        public DateTime? ApprovalDate { get; set; }
        public int? ApprovedById { get; set; }

        [ForeignKey("ApprovedById")]
        public User? ApprovedBy { get; set; }
        public decimal? Overtime { get; set; }
        // ✅ New: Session type (Morning, Afternoon, WholeDay)
        [Required]
        [StringLength(20)]
        public string Session { get; set; } = "Morning";
    }

}