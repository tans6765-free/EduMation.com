namespace EduMation.Models
{
    public class HomeViewModel
    {
        public Video FeaturedVideo { get; set; }
        public List<Video> RecommendedVideos { get; set; }
        public List<Video> Videos { get; set; }
    }
}