using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;

namespace EduMation.Models
{
    public class ApplicationUser : IdentityUser
    {
        public List<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        public List<Favorites> Favorites { get; set; } = new List<Favorites>();
        public List<WatchHistory> WatchHistory { get; set; } = new List<WatchHistory>();
        public List<Profile> Profiles { get; set; } = new List<Profile>();
    }
}