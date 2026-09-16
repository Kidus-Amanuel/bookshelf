using System.Security.Claims;
using BookShelf.Data;
using BookShelf.Models;
using BookShelf.Services;
using BookShelf.ViewModels.Authors;
using BookShelf.ViewModels.Books;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Controllers;

[Authorize]
[Route("author")]
public class AuthorController : Controller
{
    private readonly AppDbContext _context;
    private readonly IBookService _bookService;
    private readonly IAuthService _authService;
    private readonly IOrderService _orderService;
    private readonly ILoanService _loanService;
    private readonly ILogger<AuthorController> _logger;

    public AuthorController(
        AppDbContext context,
        IBookService bookService,
        IAuthService authService,
        IOrderService orderService,
        ILoanService loanService,
        ILogger<AuthorController> logger)
    {
        _context = context;
        _bookService = bookService;
        _authService = authService;
        _orderService = orderService;
        _loanService = loanService;
        _logger = logger;
    }

    // ========== MY BOOKS ==========

    [HttpGet("my-books")]
    public async Task<IActionResult> MyBooks()
    {
        var userId = CurrentUserId();
        if (userId == null) return RedirectToAction("Login", "Account");

        var profile = await _authService.GetAuthorProfileAsync(userId.Value);
        if (profile == null || profile.Status != "Approved")
        {
            TempData["Error"] = "Only approved authors can manage books.";
            return RedirectToAction("Author", "Home");
        }

        var books = await _bookService.GetAuthorBooksAsync(profile.Id);
        ViewData["PenName"] = profile.PenName;
        return View("~/Views/Author/MyBooks.cshtml", books.ToList());
    }

    // ========== ADD BOOK ==========

    [HttpGet("add-book")]
    public async Task<IActionResult> AddBook()
    {
        var userId = CurrentUserId();
        if (userId == null) return RedirectToAction("Login", "Account");

        var profile = await _authService.GetAuthorProfileAsync(userId.Value);
        if (profile == null || profile.Status != "Approved")
        {
            TempData["Error"] = "Only approved authors can submit books.";
            return RedirectToAction("Author", "Home");
        }

        ViewData["PenName"] = profile.PenName;
        return View("~/Views/Author/AddBook.cshtml", new BookViewModel());
    }

    [HttpPost("add-book")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBook(BookViewModel model)
    {
        var userId = CurrentUserId();
        if (userId == null) return RedirectToAction("Login", "Account");

        var profile = await _authService.GetAuthorProfileAsync(userId.Value);
        if (profile == null || profile.Status != "Approved")
        {
            TempData["Error"] = "Only approved authors can submit books.";
            return RedirectToAction("Author", "Home");
        }

        if (!ModelState.IsValid)
        {
            ViewData["PenName"] = profile.PenName;
            return View("~/Views/Author/AddBook.cshtml", model);
        }

        try
        {
            var book = await _bookService.CreateBookAsync(
                profile.Id,
                model.Title,
                model.Description,
                model.ISBN,
                model.CoverImageUrl,
                model.Price,
                model.IsForSale,
                model.IsForLoan,
                model.StockForSale,
                model.StockForLoan
            );

            TempData["Success"] = $"\"{book.Title}\" submitted for admin review.";
            return RedirectToAction(nameof(MyBooks));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating book for author {AuthorId}", profile.Id);
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewData["PenName"] = profile.PenName;
            return View("~/Views/Author/AddBook.cshtml", model);
        }
    }

    // ========== EDIT PROFILE ==========

    [HttpGet("edit-profile")]
    public async Task<IActionResult> EditProfile()
    {
        var userId = CurrentUserId();
        if (userId == null) return RedirectToAction("Login", "Account");

        var profile = await _authService.GetAuthorProfileAsync(userId.Value);
        if (profile == null)
        {
            TempData["Error"] = "You don't have an author profile yet.";
            return RedirectToAction("Author", "Home");
        }

        var vm = new EditAuthorProfileViewModel
        {
            PenName = profile.PenName,
            Bio = profile.Bio
        };

        ViewData["Status"] = profile.Status;
        return View("~/Views/Author/EditProfile.cshtml", vm);
    }

    [HttpPost("edit-profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProfile(EditAuthorProfileViewModel model)
    {
        var userId = CurrentUserId();
        if (userId == null) return RedirectToAction("Login", "Account");

        var profile = await _authService.GetAuthorProfileAsync(userId.Value);
        if (profile == null)
        {
            TempData["Error"] = "You don't have an author profile yet.";
            return RedirectToAction("Author", "Home");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Status"] = profile.Status;
            return View("~/Views/Author/EditProfile.cshtml", model);
        }

        profile.PenName = model.PenName;
        profile.Bio = model.Bio;
        await _context.SaveChangesAsync();

        // Refresh the AuthorStatus claim so the Authorize policy stays in sync
        if (User.Identity is System.Security.Claims.ClaimsIdentity identity)
        {
            var existing = identity.FindFirst("AuthorStatus");
            if (existing != null) identity.RemoveClaim(existing);
            identity.AddClaim(new Claim("AuthorStatus", profile.Status));
        }

        TempData["Success"] = "Author profile updated.";
        return RedirectToAction(nameof(EditProfile));
    }

    // ========== SALES & LOANS ==========

    [HttpGet("sales")]
    public async Task<IActionResult> Sales()
    {
        var userId = CurrentUserId();
        if (userId == null) return RedirectToAction("Login", "Account");

        var profile = await _authService.GetAuthorProfileAsync(userId.Value);
        if (profile == null || profile.Status != "Approved")
        {
            TempData["Error"] = "Only approved authors can view sales reports.";
            return RedirectToAction("Author", "Home");
        }

        var books = (await _bookService.GetAuthorBooksAsync(profile.Id)).ToList();
        var bookIds = books.Select(b => b.Id).ToHashSet();

        // Sales: order items for these books (counts only paid+ orders)
        var orderItems = await _context.OrderItems
            .Where(oi => bookIds.Contains(oi.BookId))
            .Include(oi => oi.Order)
            .ToListAsync();

        var paidItems = orderItems
            .Where(oi => oi.Order != null &&
                         (oi.Order.Status == "Paid" || oi.Order.Status == "Shipped" || oi.Order.Status == "Delivered"))
            .ToList();

        // Loans: for these books (any status)
        var loans = await _context.Loans
            .Where(l => bookIds.Contains(l.BookId))
            .ToListAsync();

        var performance = books.Select(b =>
        {
            var myItems = paidItems.Where(oi => oi.BookId == b.Id).ToList();
            var myLoans = loans.Where(l => l.BookId == b.Id).ToList();

            return new BookSalesPerformanceItem
            {
                BookId = b.Id,
                Title = b.Title,
                CoverImageUrl = b.CoverImageUrl,
                Price = b.Price ?? 0,
                CopiesSold = myItems.Sum(oi => oi.Quantity),
                TotalSalesRevenue = myItems.Sum(oi => oi.Quantity * oi.UnitPrice),
                TimesLoaned = myLoans.Count,
                Status = b.Status
            };
        })
        .OrderByDescending(p => p.TotalSalesRevenue)
        .ToList();

        var vm = new AuthorSalesViewModel
        {
            PenName = profile.PenName,
            TotalCopiesSold = performance.Sum(p => p.CopiesSold),
            TotalRevenue = performance.Sum(p => p.TotalSalesRevenue),
            TotalTimesLoaned = performance.Sum(p => p.TimesLoaned),
            BookPerformance = performance
        };

        return View("~/Views/Author/Sales.cshtml", vm);
    }

    private int? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : (int?)null;
    }
}