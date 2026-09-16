using BookShelf.Models;

namespace BookShelf.ViewModels.Customer;

public class CustomerActivityViewModel
{
    public string CustomerName { get; set; } = string.Empty;

    public int ActiveLoansCount { get; set; }

    public int BooksPurchasedCount { get; set; }

    public decimal FinesOwed { get; set; }

    public UserSubscription? Subscription { get; set; }

    public int DaysRemaining { get; set; }

    public IEnumerable<Loan> Loans { get; set; } = [];

    public IEnumerable<Order> Orders { get; set; } = [];
}
