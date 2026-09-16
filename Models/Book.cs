using System.ComponentModel.DataAnnotations.Schema;

namespace BookShelf.Models;

public class Book
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ISBN { get; set; } = string.Empty;
    
    public int CategoryId { get; set; }

    public string CoverImageUrl { get; set; } = string.Empty;

    public int? AuthorProfileId { get; set; }

    public decimal? Price { get; set; }

    public bool IsForSale { get; set; } = true;

    public bool IsForLoan { get; set; } = false;

    public int StockForSale { get; set; } = 0;

    public int StockForLoan { get; set; } = 0;

    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Delisted

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public AuthorProfile? AuthorProfile { get; set; }


    // Navigation properties
    public ICollection<OrderItem> OrderItems { get; set; } = [];
    public ICollection<Loan> Loans { get; set; } = [];
}
