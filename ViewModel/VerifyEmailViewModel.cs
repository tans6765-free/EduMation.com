using System.ComponentModel.DataAnnotations;

namespace EduMation.ViewModels
{
    public class VerifyEmailViewModel
    {
        [Required, RegularExpression("^\\d{6}$", ErrorMessage = "Enter the six-digit code from your email.")]
        [Display(Name = "Verification code")]
        public string Code { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
