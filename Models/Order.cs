using System.ComponentModel.DataAnnotations.Schema;

namespace BookShelf.Models;

public class Order
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string Status { get; set; } = "Pending"; // Pending, Paid, Shipped, Delivered, Cancelled

    public string Channel { get; set; } = "Online"; // Online, Local

    public int? BranchId { get; set; }

    public int? ProcessedByStaffId { get; set; }

    public decimal TotalAmount { get; set; }

    /// <summary>Pre-discount subtotal. TotalAmount may be reduced by subscription discount; Subtotal stays raw.</summary>
    public decimal Subtotal { get; set; }

    /// <summary>Subscription discount applied at checkout. Positive percentage value (e.g. 10 = 10%).</summary>
    public decimal DiscountPercent { get; set; }

    public User User { get; set; } = null!;

    public Branch? Branch { get; set; }

    // Navigation properties
    public ICollection<OrderItem> OrderItems { get; set; } = [];
    public Payment? Payment { get; set; }
}
