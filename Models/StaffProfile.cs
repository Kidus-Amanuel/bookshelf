using System.ComponentModel.DataAnnotations.Schema;

namespace BookShelf.Models;

public class StaffProfile
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int BranchId { get; set; }

    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public User User { get; set; } = null!;

    public Branch Branch { get; set; } = null!;
}
