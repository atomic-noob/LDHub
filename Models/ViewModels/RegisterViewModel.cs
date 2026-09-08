using System.ComponentModel.DataAnnotations;

namespace LDHub.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        public string FirstName { get; set; } = string.Empty;

        public string? MiddleName { get; set; }

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string? ContactNumber { get; set; }

        [Required]
        public string Gender { get; set; } = string.Empty;

        [Required]
        public int Age { get; set; }

        [Required]
        public string Position { get; set; } = string.Empty;

        public string? Designation { get; set; }

        public int? SalaryGrade { get; set; }

        public int? RegionId { get; set; }
        public int? OfficeId { get; set; }
        public int? DivisionId { get; set; }

        [Display(Name = "Profile Photo")]
        public IFormFile? Photo { get; set; }
    }
}