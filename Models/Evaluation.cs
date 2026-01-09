using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class Evaluation
    {
        [Key]
        public int EvaluationId { get; set; }

        [Required]
        public int InternId { get; set; }
        [ForeignKey("InternId")]
        public User Intern { get; set; }

        public int? ReviewedById { get; set; }
        [ForeignKey("ReviewedById")]
        public User? ReviewedBy { get; set; }

        [Required]
        public string OriginalFilePath { get; set; }   // File uploaded by Intern
        public string? ReviewedFilePath { get; set; }  // File uploaded by Supervisor/Admin

        [Required]
        public string Status { get; set; } = "Submitted";
        // Submitted → Reviewed → Completed

        public DateTime UploadDate { get; set; } = DateTime.Now;
        public DateTime? ReviewedDate { get; set; }
    }
}
