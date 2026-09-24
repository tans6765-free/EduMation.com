using System.ComponentModel.DataAnnotations;

namespace EduMation.Models
{
    public class Video
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Genre { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        public string? ThumbnailUrl { get; set; }

        public string? VideoUrl { get; set; }

        public DateTime UploadDate { get; set; }
    }
}