using System.ComponentModel.DataAnnotations;

namespace BookShelf.ViewModels.Loans;

public class LoanViewModel
{
    public int Id { get; set; }

    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string CoverImageUrl { get; set; } = string.Empty;

    public int UserId { get; set; }
    public string Channel { get; set; } = "Online";
    public int? BranchId { get; set; }

    [Range(1, 30, ErrorMessage = "Loan duration must be between 1 and 30 days.")]
    public int Days { get; set; } = 7;

    [Range(0, 1000, ErrorMessage = "Price per day must be 0 or more.")]
    public decimal PricePerDay { get; set; }

    [Range(0, 1000, ErrorMessage = "Penalty per day must be 0 or more.")]
    public decimal PenaltyPerDay { get; set; } = 0.50m;

    public DateTime BorrowedAt { get; set; } = DateTime.UtcNow;
    public DateTime DueAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public bool IsReturned { get; set; }
    public decimal FineAmount { get; set; }

    public decimal Total => PricePerDay * Days;
}
