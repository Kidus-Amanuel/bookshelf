namespace BookShelf.ViewModels.Auth;

public class EmailConfirmationViewModel
{
    public string Email { get; set; } = "";

    // false = "we sent you a link, go check your inbox"
    // true  = the user has just clicked that link and this is the result page
    public bool IsConfirmed { get; set; } = false;
}
