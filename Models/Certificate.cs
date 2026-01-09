using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    [Table("Certificates")]
    public class Certificate
    {
        [Key]
        public int CertificateId { get; set; }

        public int? UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        public User User { get; set; }

        [Required]
        public DateTime DateIssued { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string FilePath { get; set; }  // Path of the generated PDF
    }
}
