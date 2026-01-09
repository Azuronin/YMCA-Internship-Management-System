using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "First name is required.")]
        public string FirstName { get; set; } = string.Empty;

        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        public string LastName { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        //[CustomValidation(typeof(User), nameof(ValidateBirthdate))]
        public DateTime Birthdate { get; set; } = DateTime.MaxValue;

        public static ValidationResult? ValidateBirthdate(DateTime birthdate, ValidationContext context)
        {
            var today = DateTime.Today;
            if (birthdate == DateTime.MinValue || birthdate > today)
                return new ValidationResult("Please select a valid birthdate.");

            var age = today.Year - birthdate.Year;
            if (birthdate > today.AddYears(-age)) age--;

            return age < 18
                ? new ValidationResult("You must be at least 18 years old.")
                : ValidationResult.Success;
        }

        //[Required]
        public int Age { get; set; }

        //[Required(ErrorMessage = "Gender is required.")]
        public string? Gender { get; set; } = string.Empty;

        //[Required(ErrorMessage = "Contact number is required.")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "Contact number must start with 09 and be 11 digits.")]
        public string? ContactNumber { get; set; } = string.Empty;

        //[Required(ErrorMessage = "Position is required.")]
        public string? Position { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*[a-z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]+$",
        ErrorMessage = "Password must contain at least one uppercase, one lowercase, one number, and one special character")]
        public string Password { get; set; } = string.Empty;

        [NotMapped]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [NotMapped]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*[a-z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]+$",
        ErrorMessage = "Password must contain at least one uppercase, one lowercase, one number, and one special character")]
        [MinLength(8, ErrorMessage = "New password must be at least 8 characters.")]
        public string? NewPassword { get; set; }

        [NotMapped]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string? ConfirmNewPassword { get; set; }

        [NotMapped]
        public string? CurrentPassword { get; set; }

        public string? Course { get; set; }
        public string? School { get; set; }

        [Range(1, 2000, ErrorMessage = "Hours to render must be a positive number.")]
        public int? HoursToRender { get; set; }

        [Required]
        [RegularExpression("Approved|Disapproved|Pending", ErrorMessage = "Invalid status.")]
        public string Status { get; set; } = "Pending";
        // Add to your User.cs model
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? Address { get; set; }

        public string? ProfileImagePath { get; set; } = "/images/default.png";    
        public string? OriginalProfileFileName { get; set; }
        public decimal TotalRenderedHours { get; set; }

        public string? SecretQuestion { get; set; }

        [StringLength(200, ErrorMessage = "Answer cannot exceed 200 characters")]
        public string? SecretAnswer { get; set; }
        public virtual ICollection<TasksManage> TasksManage { get; set; } = new List<TasksManage>();
    }
}
