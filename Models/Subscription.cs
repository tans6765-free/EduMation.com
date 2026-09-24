using System;
using System.ComponentModel.DataAnnotations;

namespace EduMation.Models
{
    public class Subscription
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        public string Plan { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero.")]
        public decimal Price { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "MaxVideos must be non-negative.")]
        public int MaxVideos { get; set; }

        [Required]
        public bool IsActive { get; set; }

        public int LastWatchedMonth { get; set; }

        public int LastWatchedYear { get; set; }

        public int TotalWatched { get; set; }
        public ApplicationUser User { get; internal set; }
    }
}