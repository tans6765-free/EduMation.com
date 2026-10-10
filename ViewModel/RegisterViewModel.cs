using System.ComponentModel.DataAnnotations;

namespace EduMation.ViewModels
{
    public class RegisterViewModel
    {
        [Required, StringLength(60, MinimumLength = 3)]
        [RegularExpression("^[a-zA-Z0-9._-]+$", ErrorMessage = "Use letters, numbers, dots, underscores, or hyphens for your User ID.")]
        [Display(Name = "User ID")]
        public string UserId { get; set; } = string.Empty;

        [Required, StringLength(120, MinimumLength = 2)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(80)]
        public string District { get; set; } = string.Empty;

        [StringLength(40), Phone]
        [Display(Name = "Contact number (optional)")]
        public string? ContactNumber { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Compare("Password", ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Choose your class")]
        [Range(1, int.MaxValue, ErrorMessage = "Choose a valid class")]
        public int? LearningClassId { get; set; }
    }
}
