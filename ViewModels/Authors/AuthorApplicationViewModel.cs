using System.ComponentModel.DataAnnotations;

namespace BookShelf.ViewModels.Authors;

public class AuthorApplicationViewModel
{
    [Required(ErrorMessage = "Please enter a pen name.")]
    [StringLength(100)]
    public string PenName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tell us a bit about yourself.")]
    [StringLength(1000)]
    public string Bio { get; set; } = string.Empty;

    // Populated by the controller, not posted by the form
    public string? ExistingStatus { get; set; } // null, "Pending", "Rejected"
    public DateTime? AppliedAt { get; set; }
}