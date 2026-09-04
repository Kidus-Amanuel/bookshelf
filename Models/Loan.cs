using System.ComponentModel.DataAnnotations.Schema;

namespace BookShelf.Models;

public class Loan
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int UserId { get; set; }
    public DateTime BorrowedAt { get; set; } = DateTime.Now;
    public DateTime? ReturnedAt { get; set; }
    public bool IsReturned { get; set; }

    [ForeignKey(nameof(BookId))]
    public Book Book { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
