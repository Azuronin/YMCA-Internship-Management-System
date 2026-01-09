using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    [Table("TasksManage")]
    public class TasksManage
    {
        [Key]
        public int TaskId { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        [Required]
        [StringLength(1000)]
        public string Description { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        public DateTime DueDate { get; set; }

        public string? ImagePath { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = "Pending";

        [Required]
        public int AssignedToId { get; set; }
        [ForeignKey(nameof(AssignedToId))]
        public User AssignedTo { get; set; }

        // ✅ The person who assigned the task
        public int? AssignedById { get; set; }
        [ForeignKey(nameof(AssignedById))]
        public User? AssignedBy { get; set; }

        // New properties for better task management
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? AcceptedDate { get; set; }  // When intern accepted task
        public DateTime? CompletedDate { get; set; }

        [StringLength(500)]
        public string? ProgressNotes { get; set; }
        public int Priority { get; set; } = 2; // 1: High, 2: Medium, 3: Low
    }
}