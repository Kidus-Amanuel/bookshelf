namespace BookShelf.ViewModels.Loans;

public class LoanViewModel
{
    public int BookId { get; set; }
    public int UserId { get; set; }
    public DateTime BorrowedAt { get; set; } = DateTime.Now;
    public DateTime? ReturnedAt { get; set; }
}
