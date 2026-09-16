using System.Security.Claims;
using BookShelf.Data;
using BookShelf.Models;
using BookShelf.Services;
using BookShelf.ViewModels.Loans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Controllers;

[Authorize]
public class LoansController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILoanService _loanService;
    private readonly ILogger<LoansController> _logger;

    public LoansController(AppDbContext context, ILoanService loanService, ILogger<LoansController> logger)
    {
        _context = context;
        _loanService = loanService;
        _logger = logger;
    }

    // Admin: all loans across the system
    [HttpGet]
    public async Task<IActionResult> Index(string? status = null)
    {
        var loans = await _loanService.GetAllLoansAsync(status);
        ViewBag.CurrentStatus = status ?? "all";
        return View(loans);
    }

    // Customer: my loans
    [HttpGet("my-loans")]
    public async Task<IActionResult> MyLoans()
    {
        var userId = CurrentUserId();
        if (userId == null) return Challenge();
        var loans = await _loanService.GetUserLoansAsync(userId.Value);
        return View(loans);
    }

    // Borrow flow — GET form
    [HttpGet]
    public async Task<IActionResult> Borrow(int bookId)
    {
        var userId = CurrentUserId();
        if (userId == null) return Challenge();

        var book = await _context.Books
            .Include(b => b.AuthorProfile)
            .FirstOrDefaultAsync(b => b.Id == bookId);

        if (book == null || !book.IsForLoan || book.StockForLoan <= 0)
        {
            return NotFound("Book is not available for loan.");
        }

        // Price per day: derive from book price; loan is 1% of book price per day, min $0.50.
        var pricePerDay = Math.Round(((book.Price ?? 10m) * 0.01m), 2);
        if (pricePerDay < 0.50m) pricePerDay = 0.50m;

        var model = new LoanViewModel
        {
            BookId = bookId,
            BookTitle = book.Title,
            AuthorName = book.AuthorProfile?.PenName ?? "Unknown author",
            CoverImageUrl = book.CoverImageUrl,
            UserId = userId.Value,
            Channel = "Online",
            BranchId = null,
            Days = 7,
            PricePerDay = pricePerDay,
            PenaltyPerDay = LoanService.DailyFineAmount,
            DueAt = DateTime.UtcNow.AddDays(7)
        };

        return View(model);
    }

    // Borrow flow — POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Borrow(LoanViewModel model)
    {
        var userId = CurrentUserId();
        if (userId == null) return Challenge();

        // Re-derive server-side: don't trust hidden inputs.
        var book = await _context.Books
            .Include(b => b.AuthorProfile)
            .FirstOrDefaultAsync(b => b.Id == model.BookId);

        if (book == null || !book.IsForLoan || book.StockForLoan <= 0)
        {
            ModelState.AddModelError(string.Empty, "Book is no longer available for loan.");
            return View(model);
        }

        // Re-validate user from session, not from model.
        ModelState.Remove(nameof(LoanViewModel.UserId));
        model.UserId = userId.Value;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var pricePerDay = model.PricePerDay;
            if (pricePerDay <= 0)
            {
                pricePerDay = Math.Round(((book.Price ?? 10m) * 0.01m), 2);
                if (pricePerDay < 0.50m) pricePerDay = 0.50m;
            }

            var loan = await _loanService.CreateLoanAsync(
                userId.Value,
                model.BookId,
                model.Channel,
                model.BranchId,
                loanPeriodDays: model.Days,
                pricePerDay: pricePerDay
            );

            _logger.LogInformation($"User {userId} borrowed book {model.BookId} for {model.Days} days");
            TempData["Message"] = $"Borrowed '{book.Title}' for {model.Days} day(s). Due {loan.DueAt:yyyy-MM-dd}.";
            return RedirectToAction(nameof(MyLoans));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating loan: {ex.Message}");
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    // Mark a loan as returned
    [HttpPost("return/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Return([FromRoute] int id)
    {
        var userId = CurrentUserId();
        if (userId == null) return Challenge();

        var loan = await _loanService.GetLoanByIdAsync(id);
        if (loan == null || (loan.UserId != userId.Value && !User.IsInRole("Admin")))
        {
            return Forbid();
        }

        var success = await _loanService.ReturnLoanAsync(id);
        if (!success)
        {
            return NotFound();
        }

        var reloaded = await _loanService.GetLoanByIdAsync(id);
        if (reloaded?.FineAmount > 0 && !reloaded.FinePaid)
        {
            TempData["Message"] = $"Returned. Outstanding fine: ${reloaded.FineAmount:F2}";
        }

        _logger.LogInformation($"Loan {id} returned by user {userId}");
        return RedirectToAction(nameof(Index));
    }

    // Loan details (customer / admin)
    [HttpGet("details/{id:int}")]
    public async Task<IActionResult> Details([FromRoute] int id)
    {
        var loan = await _loanService.GetLoanByIdAsync(id);
        if (loan == null)
        {
            return NotFound();
        }

        var userId = CurrentUserId();
        if (userId != null && loan.UserId != userId.Value && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var fine = await _loanService.CalculateFineAsync(id);
        ViewBag.CurrentFine = fine;

        return View(loan);
    }

    // Pay fine
    [HttpPost("pay-fine/{loanId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PayFine([FromRoute] int loanId, decimal amount)
    {
        var loan = await _loanService.GetLoanByIdAsync(loanId);
        if (loan == null)
        {
            return NotFound();
        }

        var fine = await _loanService.CalculateFineAsync(loanId);
        if (amount < fine)
        {
            return BadRequest($"Insufficient payment. Due amount: ${fine:F2}");
        }

        var payment = new Payment
        {
            LoanId = loanId,
            Amount = amount,
            Status = "Completed",
            PaymentMethod = "Card",
            PaidAt = DateTime.UtcNow
        };

        _context.Payments.Add(payment);
        await _loanService.MarkFinePaidAsync(loanId);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Fine paid for loan {loanId}: ${amount}");
        return RedirectToAction(nameof(Details), new { id = loanId });
    }

    // Admin: view overdue loans
    [Authorize(Roles = "Admin")]
    [HttpGet("admin/overdue")]
    public async Task<IActionResult> OverdueLoans()
    {
        var overdueLoans = await _loanService.GetOverdueLoansAsync();
        return View(overdueLoans);
    }

    private int? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : (int?)null;
    }
}
