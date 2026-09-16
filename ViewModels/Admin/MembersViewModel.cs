using BookShelf.Models;

namespace BookShelf.ViewModels.Admin;

public class MembersViewModel
{
    public List<User> Users { get; set; } = [];

    public List<Role> Roles { get; set; } = [];

    public string? FilterRole { get; set; }

    public string? SearchTerm { get; set; }

    public int TotalCount { get; set; }

    public int CustomerCount { get; set; }

    public int AdminCount { get; set; }

    public int StaffCount { get; set; }

    public int ApprovedAuthorCount { get; set; }

    public int PendingAuthorCount { get; set; }
}