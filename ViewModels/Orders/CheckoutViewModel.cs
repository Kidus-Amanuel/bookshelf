namespace BookShelf.ViewModels.Orders;

public class CheckoutItemViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;
}

public class CheckoutViewModel
{
    public int? OrderId { get; set; }
    public int? PlanId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public List<CheckoutItemViewModel> Items { get; set; } = [];
    public decimal Subtotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = "Card";
    public string Channel { get; set; } = "Online";
    public string? BranchName { get; set; }
    public bool IsSubscription => PlanId.HasValue;
}
