namespace BookShelf.Services;

public class BookService
{
    public string GetStatusLabel(bool isAvailable)
    {
        return isAvailable ? "Available" : "Unavailable";
    }
}
