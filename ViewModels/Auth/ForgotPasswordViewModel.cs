using System.ComponentModel.DataAnnotations;

namespace BookShelf.ViewModels.Auth;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Enter your email address")]
    [EmailAddress(ErrorMessage = "Enter a valid email address")]
    public string Email { get; set; } = "";
}
