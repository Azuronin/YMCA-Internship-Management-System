using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class Documents
    {
        [Key]
        public int DocumentId { get; set; }

        // FK to the user who submitted
        public int UserId { get; set; }
        public User User { get; set; } // Navigation property (optional)

        public string FileName { get; set; }
        public string FilePath { get; set; }

        public DateTime UploadDate { get; set; }

        // Status tracking
        public string Status { get; set; } // "Pending", "Approved", "Rejected"

        public string DocumentType { get; set; } // e.g., "MOA", "Endorsement", "Acceptance", etc.

        // Supervisor/Trainer or admin who approved
        public int? ApprovedById { get; set; }

        [ForeignKey("ApprovedById")]
        public User? ApprovedBy { get; set; }
    }
}
