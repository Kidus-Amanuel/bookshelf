using BookShelf.Models;

namespace BookShelf.ViewModels.Subscriptions;

public class SubscriptionsViewModel
{
    public UserSubscription? Current { get; set; }

    public IEnumerable<SubscriptionPlan> AvailablePlans { get; set; } = [];

    public int ActiveLoans { get; set; }

    public int DaysRemaining { get; set; }
}