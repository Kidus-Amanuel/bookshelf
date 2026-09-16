using System.ComponentModel.DataAnnotations;

namespace BookShelf.ViewModels.Authors;

public class EditAuthorProfileViewModel
{
    [Required(ErrorMessage = "Please enter your pen name.")]
    [StringLength(100, ErrorMessage = "Pen name cannot exceed 100 characters.")]
    public string PenName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter a brief biography.")]
    [StringLength(1000, ErrorMessage = "Biography cannot exceed 1000 characters.")]
    public string Bio { get; set; } = string.Empty;
}
