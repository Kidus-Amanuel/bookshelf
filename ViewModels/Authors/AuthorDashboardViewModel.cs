using BookShelf.Models;

namespace BookShelf.ViewModels.Authors;

public class AuthorDashboardViewModel
{
    public int AuthorProfileId { get; set; }

    public string PenName { get; set; } = string.Empty;

    public string Bio { get; set; } = string.Empty;

    public string Status { get; set; } = "Approved";

    public int PublishedBooksCount { get; set; }

    public int PendingReviewCount { get; set; }

    public int CopiesSoldCount { get; set; }

    public int TimesLoanedCount { get; set; }

    public decimal TotalSalesRevenue { get; set; }

    public List<Book> Books { get; set; } = [];
}
