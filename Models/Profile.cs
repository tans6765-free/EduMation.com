using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

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

        public string LastName { get; set; }

        [NotMapped, Display(Name = "Full name")]
        public string FullName
        {
            get => $"{FirstName} {LastName}".Trim();
            set
            {
                var words = (value ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                FirstName = words.FirstOrDefault() ?? string.Empty;
                LastName = words.Length > 1 ? string.Join(' ', words.Skip(1)) : string.Empty;
            }
        }

        [Required, StringLength(80)]
        public string District { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        public string ContactNumber { get; set; } = string.Empty;

        [Range(8, 120)]
        public int? Age { get; set; }

        public string Address { get; set; } = string.Empty;

        public string ProfilePicturePath { get; set; } = "/images/default-profile.png";

        public int? LearningClassId { get; set; }
        public LearningClass? LearningClass { get; set; }
        public int? AcademicYearId { get; set; }
        public AcademicYear? AcademicYear { get; set; }
        [StringLength(12)] public string PreferredLanguage { get; set; } = "en";
        [StringLength(1000)] public string LearningPreferences { get; set; } = string.Empty;
    }
}
