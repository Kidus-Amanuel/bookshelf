namespace BookShelf.Services;

public class LoanService
{
    public bool CanBorrow(int quantity)
    {
        return quantity > 0;
    }
}
