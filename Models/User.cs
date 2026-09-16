namespace BookShelf.Models;

public class User
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public int RoleId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Role Role { get; set; } = null!;

    // Optional: User can be an author
    public AuthorProfile? AuthorProfile { get; set; }

    // Navigation properties
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<Loan> Loans { get; set; } = [];
    public ICollection<UserSubscription> Subscriptions { get; set; } = [];
    public ICollection<AdminInvite> AdminInvitesSent { get; set; } = [];
    public StaffProfile? StaffProfile { get; set; }
}