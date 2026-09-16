using System.ComponentModel.DataAnnotations.Schema;

namespace BookShelf.Models;

public class AuthorProfile
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string PenName { get; set; } = string.Empty;

    public string Bio { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Suspended

    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ApprovedAt { get; set; }

    public int? ApprovedByAdminId { get; set; }

    public User User { get; set; } = null!;

    public User? ApprovedByAdmin { get; set; }

    // Navigation properties
    public ICollection<Book> Books { get; set; } = [];
}


