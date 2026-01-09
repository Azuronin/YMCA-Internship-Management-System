    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    namespace IMS.Models
    {
        public class Announcements
        {
            [Key]
            public int AnnouncementId { get; set; }

            [Required]
            public string Title { get; set; }

            [Required]
            public string Message { get; set; }

            [Required]
            public string Type { get; set; } // Deadline, Absence, Reminder, General

            public DateTime DatePosted { get; set; } = DateTime.Now;

            public int? PosterId { get; set; }
            [ForeignKey("PosterId")]
            public User? Poster { get; set; }
        }
    }
