using System.ComponentModel.DataAnnotations.Schema;

namespace BookShelf.Models;

public class Loan
{
    public int Id { get; set; }

    public int BookId { get; set; }

    public int UserId { get; set; }

    public DateTime BorrowedAt { get; set; } = DateTime.UtcNow;

    public DateTime DueAt { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public bool IsReturned { get; set; } = false;

    public string Channel { get; set; } = "Online"; // Online, Local

    public int? BranchId { get; set; }

    public decimal FineAmount { get; set; } = 0;

    public bool FinePaid { get; set; } = false;

    /// <summary>Days the borrower chose at checkout. Drives DueAt = BorrowedAt + Days.</summary>
    public int LoanPeriodDays { get; set; } = 14;

    /// <summary>Price-per-day snapshot at borrow time. Drives TotalAmount.</summary>
    public decimal PricePerDay { get; set; } = 0m;

    /// <summary>Precomputed total = PricePerDay * LoanPeriodDays, snapshotted at borrow time.</summary>
    public decimal TotalAmount { get; set; } = 0m;

    public Book Book { get; set; } = null!;

    public User User { get; set; } = null!;

    public Branch? Branch { get; set; }
}
