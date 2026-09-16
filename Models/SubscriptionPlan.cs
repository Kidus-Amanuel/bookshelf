namespace BookShelf.Models;

public class SubscriptionPlan
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int DurationInDays { get; set; }

    public int MaxActiveLoans { get; set; } = 5;

    public decimal DiscountPercentOnPurchases { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<UserSubscription> UserSubscriptions { get; set; } = [];
}
