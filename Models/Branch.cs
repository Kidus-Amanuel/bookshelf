namespace BookShelf.Models;

public class Branch
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<Loan> Loans { get; set; } = [];
    public ICollection<StaffProfile> Staff { get; set; } = [];
}
