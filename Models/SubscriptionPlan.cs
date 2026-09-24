namespace EduMation.Models
{
    public class SubscriptionPlan
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int VideoLimit { get; set; }

        public SubscriptionPlan(string name, string description, decimal price, int videoLimit)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Price = price;
            VideoLimit = videoLimit;
        }

        public SubscriptionPlan(string name, string description)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Price = 0m; // Default price
            VideoLimit = 0; // Default unlimited
        }
    }
}