using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    [Table("Reports")]
    public class InternReport
    {
        [Key]
        public int ReportId { get; set; }

        [Required]
        [StringLength(2000)]
        public string? Content { get; set; }   // 📝 Intern’s written report

        public DateTime DateSubmitted { get; set; } = DateTime.Now;

        // NEW FIELD: Report Type (Task Report or Task Completion)
        [Required]
        [StringLength(50)]
        public string ReportType { get; set; } = "Task Report";
        [Required]
        public string? FilePath { get; set; }

        // 🔗 Link to Task
        [Required]
        public int TaskId { get; set; }
        [ForeignKey(nameof(TaskId))]
        public TasksManage? Task { get; set; }

        public int? UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }
    }

}

