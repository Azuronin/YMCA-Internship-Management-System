using System.ComponentModel.DataAnnotations;

namespace IMS.Models
{
    public class Applicants
    {
        [Key]
        public int ApplicantId { get; set; }
        [Required, EmailAddress]
        public string Email { get; set; } // ✅ Identify applicant before account creation

        public string FullName { get; set; } 
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public DateTime UploadDate { get; set; }

        public bool IsSeen { get; set; } = false;
        public string Status { get; set; } = "Pending";
    }

}
