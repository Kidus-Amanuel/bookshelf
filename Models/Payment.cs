using System.ComponentModel.DataAnnotations.Schema;

namespace BookShelf.Models;

public class Payment
{
    public int Id { get; set; }

    public int? OrderId { get; set; }

    public int? SubscriptionId { get; set; }

    public int? LoanId { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = "Card";

    public string Status { get; set; } = "Pending"; // Pending, Completed, Failed, Refunded

    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    public Order? Order { get; set; }

    public UserSubscription? Subscription { get; set; }

    public Loan? Loan { get; set; }
}
