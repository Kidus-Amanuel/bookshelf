using BookShelf.Models;

namespace BookShelf.ViewModels.Customer;

public class CustomerDashboardViewModel
{
    public string CustomerName { get; set; } = string.Empty;

    public int ActiveLoansCount { get; set; }

    public decimal FinesOwed { get; set; }

    public UserSubscription? Subscription { get; set; }

    public int DaysRemaining { get; set; }

    public string PlanName => Subscription?.Plan?.Name ?? "No active plan";

    public IEnumerable<Book> TrendingBooks { get; set; } = [];

    public IEnumerable<Book> NewReleases { get; set; } = [];
}
