using BookShelf.Models;

namespace BookShelf.ViewModels.Admin;

public class AdminDashboardViewModel
{
    public List<Book> PendingBooks { get; set; } = [];

    public List<AuthorProfile> PendingAuthors { get; set; } = [];

    public List<Order> RecentOrders { get; set; } = [];
}