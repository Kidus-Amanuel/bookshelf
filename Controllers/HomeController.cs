using System.Security.Claims;
using BookShelf.Data;
using BookShelf.Models;
using BookShelf.Services;
using BookShelf.ViewModels.Admin;
using BookShelf.ViewModels.Authors;
using BookShelf.ViewModels.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;
    private readonly IBookService _bookService;
    private readonly ILoanService _loanService;
    private readonly IOrderService _orderService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IAuthService _authService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        AppDbContext context,
        IBookService bookService,
        ILoanService loanService,
        IOrderService orderService,
        ISubscriptionService subscriptionService,
        IAuthService authService,
        ILogger<HomeController> logger)
    {
        _context = context;
        _bookService = bookService;
        _loanService = loanService;
        _orderService = orderService;
        _subscriptionService = subscriptionService;
        _authService = authService;
        _logger = logger;
    }

    [Authorize]
    public async Task<IActionResult> Customer()
    {
        var userId = CurrentUserId();
        if (userId == null) return RedirectToAction("Login", "Account");

        var user = await _context.Users.FindAsync(userId.Value);
        var subscription = await _subscriptionService.GetActiveSubscriptionAsync(userId.Value);
        var activeLoansCount = await _loanService.GetActiveLoansCountAsync(userId.Value);
        
        var userLoans = await _loanService.GetUserLoansAsync(userId.Value);
        var finesOwed = userLoans.Where(l => !l.FinePaid).Sum(l => l.FineAmount);

        var daysRemaining = subscription != null
            ? Math.Max(0, (int)Math.Ceiling((subscription.EndDate - DateTime.UtcNow).TotalDays))
            : 0;

        var allApproved = (await _bookService.GetApprovedBooksAsync()).ToList();
        var trending = allApproved.Take(8).ToList();
        var newReleases = allApproved.OrderByDescending(b => b.CreatedAt).Take(8).ToList();

        var vm = new CustomerDashboardViewModel
        {
            CustomerName = user?.FullName ?? User.Identity?.Name ?? "Reader",
            ActiveLoansCount = activeLoansCount,
            FinesOwed = finesOwed,
            Subscription = subscription,
            DaysRemaining = daysRemaining,
            TrendingBooks = trending,
            NewReleases = newReleases
        };

        return View("Customer/Index", vm);
    }

    [Authorize]
    public async Task<IActionResult> MyActivity()
    {
        var userId = CurrentUserId();
        if (userId == null) return RedirectToAction("Login", "Account");

        var user = await _context.Users.FindAsync(userId.Value);
        var subscription = await _subscriptionService.GetActiveSubscriptionAsync(userId.Value);
        var activeLoansCount = await _loanService.GetActiveLoansCountAsync(userId.Value);
        
        var userLoans = (await _loanService.GetUserLoansAsync(userId.Value)).ToList();
        var finesOwed = userLoans.Where(l => !l.FinePaid).Sum(l => l.FineAmount);

        var daysRemaining = subscription != null
            ? Math.Max(0, (int)Math.Ceiling((subscription.EndDate - DateTime.UtcNow).TotalDays))
            : 0;

        var orders = await _context.Orders
            .Where(o => o.UserId == userId.Value)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Book)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var booksPurchasedCount = orders
            .Where(o => o.Status == "Paid" || o.Status == "Delivered" || o.Status == "Shipped")
            .SelectMany(o => o.OrderItems)
            .Sum(oi => oi.Quantity);

        var vm = new CustomerActivityViewModel
        {
            CustomerName = user?.FullName ?? User.Identity?.Name ?? "Reader",
            ActiveLoansCount = activeLoansCount,
            BooksPurchasedCount = booksPurchasedCount,
            FinesOwed = finesOwed,
            Subscription = subscription,
            DaysRemaining = daysRemaining,
            Loans = userLoans,
            Orders = orders
        };

        return View("Customer/MyActivity", vm);
    }

    [Authorize]
    public async Task<IActionResult> Author()
    {
        var userId = CurrentUserId();
        if (userId == null) return RedirectToAction("Login", "Account");

        var profile = await _authService.GetAuthorProfileAsync(userId.Value);

        // If the user has no author profile (yet), still render an empty shell so the
        // sidebar works and the user can apply via "Become an author".
        if (profile == null)
        {
            var emptyVm = new AuthorDashboardViewModel
            {
                PenName = User.Identity?.Name ?? "Reader",
                Bio = string.Empty,
                Status = "None"
            };
            return View("Author/Index", emptyVm);
        }

        var books = (await _bookService.GetAuthorBooksAsync(profile.Id)).ToList();
        var bookIds = books.Select(b => b.Id).ToHashSet();

        var published = books.Count(b => b.Status == "Approved");
        var pending = books.Count(b => b.Status == "Pending");

        // Sales: only count order items whose parent order is past "Pending"
        var paidItems = await _context.OrderItems
            .Where(oi => bookIds.Contains(oi.BookId))
            .Include(oi => oi.Order)
            .ToListAsync();

        var soldCount = paidItems
            .Where(oi => oi.Order != null &&
                         (oi.Order.Status == "Paid" || oi.Order.Status == "Shipped" || oi.Order.Status == "Delivered"))
            .Sum(oi => oi.Quantity);

        var revenue = paidItems
            .Where(oi => oi.Order != null &&
                         (oi.Order.Status == "Paid" || oi.Order.Status == "Shipped" || oi.Order.Status == "Delivered"))
            .Sum(oi => oi.Quantity * oi.UnitPrice);

        var loansCount = await _context.Loans
            .Where(l => bookIds.Contains(l.BookId))
            .CountAsync();

        var vm = new AuthorDashboardViewModel
        {
            AuthorProfileId = profile.Id,
            PenName = profile.PenName,
            Bio = profile.Bio,
            Status = profile.Status,
            PublishedBooksCount = published,
            PendingReviewCount = pending,
            CopiesSoldCount = soldCount,
            TimesLoanedCount = loansCount,
            TotalSalesRevenue = revenue,
            Books = books
        };

        return View("Author/Index", vm);
    }

    [Authorize]
    public async Task<IActionResult> Admin()
    {
        var pendingBooks = await _bookService.GetPendingBooksAsync();
        var pendingAuthors = await _context.AuthorProfiles
            .Where(ap => ap.Status == "Pending")
            .Include(ap => ap.User)
            .OrderByDescending(ap => ap.AppliedAt)
            .Take(5)
            .ToListAsync();

        var overdueLoans = await _loanService.GetOverdueLoansAsync();
        var recentOrders = (await _orderService.GetAllOrdersAsync())
            .Take(8)
            .ToList();

        ViewData["PendingBooksCount"] = pendingBooks.Count();
        ViewData["PendingAuthorsCount"] = pendingAuthors.Count;
        ViewData["ActiveLoansCount"] = await _context.Loans.CountAsync(l => !l.IsReturned);
        ViewData["OverdueLoansCount"] = overdueLoans.Count();
        ViewData["MembersCount"] = await _context.Users.CountAsync();

        var vm = new AdminDashboardViewModel
        {
            PendingBooks = pendingBooks.Take(5).ToList(),
            PendingAuthors = pendingAuthors,
            RecentOrders = recentOrders
        };

        return View("Admin/Index", vm);
    }

    private int? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : (int?)null;
    }
}
