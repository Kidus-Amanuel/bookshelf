using System.ComponentModel.DataAnnotations.Schema;

namespace BookShelf.Models;

public class UserSubscription
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int PlanId { get; set; }

    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    public DateTime EndDate { get; set; }

    public string Status { get; set; } = "Active"; // Active, Expired, Cancelled

    public bool AutoRenew { get; set; } = true;

    public User User { get; set; } = null!;

    public SubscriptionPlan Plan { get; set; } = null!;

    // Navigation properties
    public Payment? Payment { get; set; }
}
