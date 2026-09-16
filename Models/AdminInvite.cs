using System.ComponentModel.DataAnnotations.Schema;

namespace BookShelf.Models;

public class AdminInvite
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; } = false;

    public int CreatedByAdminId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User CreatedByAdmin { get; set; } = null!;
}
