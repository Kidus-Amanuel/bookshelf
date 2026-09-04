namespace BookShelf.Services;

public class AuthService
{
    public bool ValidateLogin(string email, string password)
    {
        return !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password);
    }
}
