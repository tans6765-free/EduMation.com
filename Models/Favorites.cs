using System.ComponentModel.DataAnnotations;

namespace EduMation.Models
{
    public class Favorites
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public int VideoId { get; set; }

        public Video? Video { get; set; }
        public DateTime AddedDate { get; set; } 
    }
}