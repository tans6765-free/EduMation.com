namespace EduMation.Models
{
    public interface ISubscription
    {
        int Id { get; init; }
        string Plan { get; set; }
        string UserId { get; set; }

        void Deconstruct(out int Id, out string UserId, out string Plan);
        bool Equals(object? obj);
        bool Equals(Subscription? other);
        int GetHashCode();
        string ToString();
    }
}