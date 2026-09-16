using BookShelf.Models;

namespace BookShelf.ViewModels.Customer;

public class CustomerOnboardingViewModel
{
    public string CustomerName { get; set; } = string.Empty;

    public SubscriptionPlan? FreeTrialPlan { get; set; }

    public IEnumerable<SubscriptionPlan> AvailablePlans { get; set; } = [];
}
