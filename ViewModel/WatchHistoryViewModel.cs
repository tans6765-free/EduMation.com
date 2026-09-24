namespace EduMation.ViewModels
{
    public class WatchHistoryViewModel
    {
        public EduMation.Models.Video? Video { get; set; }
        public int WatchCount { get; set; }
        public DateTime LastWatched { get; set; }
    }
}