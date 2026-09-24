using System.ComponentModel.DataAnnotations;

namespace EduMation.ViewModels
{
    public class VerifyEmailViewModel
    {
        [Required(ErrorMessage = "Verification code is required")]
        public string Code { get; set; } = string.Empty;
    }
}