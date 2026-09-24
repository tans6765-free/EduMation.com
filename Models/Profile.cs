using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace EduMation.Models
{
    public class Profile
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        public ApplicationUser User { get; set; } // Navigation property

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        public string ContactNumber { get; set; }

        public int? Age { get; set; }

        public string Address { get; set; }

        public string ProfilePicturePath { get; set; }
    }
}