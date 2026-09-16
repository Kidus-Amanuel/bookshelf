namespace BookShelf.ViewModels.Authors;

public class BookSalesPerformanceItem
{
    public int BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public decimal Price { get; set; }
    public int CopiesSold { get; set; }
    public decimal TotalSalesRevenue { get; set; }
    public int TimesLoaned { get; set; }
    public string Status { get; set; } = "Approved";
}

public class AuthorSalesViewModel
{
    public string PenName { get; set; } = string.Empty;
    public int TotalCopiesSold { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalTimesLoaned { get; set; }
    public List<BookSalesPerformanceItem> BookPerformance { get; set; } = [];
}
