namespace EduMation.Models
{
    public class WatchHistory
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int VideoId { get; set; }
        public DateTime WatchDate { get; set; }
        public int WatchCount { get; set; } // Tracks how many times the video was watched
        public int ProgressPercent { get; set; }
        public double LastPositionSeconds { get; set; }
        public DateTime? StartedAtUtc { get; set; }

        public ApplicationUser User { get; set; }
        public Video Video { get; set; }
    }
}
