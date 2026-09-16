using BookShelf.Data;
using BookShelf.Models;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Services;

public interface ILoanService
{
    Task<Loan> CreateLoanAsync(int userId, int bookId, string channel, int? branchId, int loanPeriodDays = 14, decimal pricePerDay = 0m);
    Task<bool> ReturnLoanAsync(int loanId);
    Task<Loan?> GetLoanByIdAsync(int loanId);
    Task<IEnumerable<Loan>> GetUserLoansAsync(int userId);
    Task<IEnumerable<Loan>> GetAllLoansAsync(string? status = null);
    Task<IEnumerable<Loan>> GetOverdueLoansAsync();
    Task<decimal> CalculateFineAsync(int loanId);
    Task<bool> MarkFinePaidAsync(int loanId);
    Task<int> GetActiveLoansCountAsync(int userId);
}

public class LoanService : ILoanService
{
    private readonly AppDbContext _context;
    private readonly ILogger<LoanService> _logger;
    public const decimal DailyFineAmount = 0.50m;

    public LoanService(AppDbContext context, ILogger<LoanService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Loan> CreateLoanAsync(int userId, int bookId, string channel, int? branchId, int loanPeriodDays = 14, decimal pricePerDay = 0m)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        var book = await _context.Books.FindAsync(bookId);
        if (book == null)
        {
            throw new InvalidOperationException("Book not found.");
        }

        if (!book.IsForLoan || book.StockForLoan <= 0)
        {
            throw new InvalidOperationException("Book is not available for loan.");
        }

        if (loanPeriodDays < 1 || loanPeriodDays > 30)
        {
            throw new InvalidOperationException("Loan period must be between 1 and 30 days.");
        }

        // Check subscription limits if user has active subscription
        var activeLoansCount = await GetActiveLoansCountAsync(userId);
        var subscription = await _context.UserSubscriptions
            .Where(us => us.UserId == userId && us.Status == "Active")
            .Include(us => us.Plan)
            .FirstOrDefaultAsync();

        if (subscription != null && activeLoansCount >= subscription.Plan.MaxActiveLoans)
        {
            throw new InvalidOperationException("Maximum loan limit reached for subscription.");
        }

        var now = DateTime.UtcNow;
        var loan = new Loan
        {
            UserId = userId,
            BookId = bookId,
            BorrowedAt = now,
            DueAt = now.AddDays(loanPeriodDays),
            Channel = channel,
            BranchId = branchId,
            IsReturned = false,
            FineAmount = 0,
            LoanPeriodDays = loanPeriodDays,
            PricePerDay = pricePerDay,
            TotalAmount = pricePerDay * loanPeriodDays
        };

        book.StockForLoan--;
        _context.Loans.Add(loan);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Loan created for user {userId}, book {bookId} for {loanPeriodDays} days");
        return loan;
    }

    public async Task<bool> ReturnLoanAsync(int loanId)
    {
        var loan = await _context.Loans.FindAsync(loanId);
        if (loan == null)
        {
            return false;
        }

        loan.IsReturned = true;
        loan.ReturnedAt = DateTime.UtcNow;

        // Calculate fine if overdue
        if (loan.ReturnedAt > loan.DueAt)
        {
            var daysOverdue = (int)Math.Ceiling((loan.ReturnedAt.Value - loan.DueAt).TotalDays);
            if (daysOverdue < 1) daysOverdue = 1;
            loan.FineAmount = daysOverdue * DailyFineAmount;
        }

        var book = await _context.Books.FindAsync(loan.BookId);
        if (book != null)
        {
            book.StockForLoan++;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation($"Loan {loanId} returned");

        return true;
    }

    public async Task<Loan?> GetLoanByIdAsync(int loanId)
    {
        return await _context.Loans
            .Include(l => l.Book)
                .ThenInclude(b => b.AuthorProfile)
            .Include(l => l.User)
            .Include(l => l.Branch)
            .FirstOrDefaultAsync(l => l.Id == loanId);
    }

    public async Task<IEnumerable<Loan>> GetUserLoansAsync(int userId)
    {
        return await _context.Loans
            .Where(l => l.UserId == userId)
            .Include(l => l.Book)
            .Include(l => l.Branch)
            .OrderByDescending(l => l.BorrowedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Loan>> GetAllLoansAsync(string? status = null)
    {
        IQueryable<Loan> query = _context.Loans
            .Include(l => l.Book)
            .Include(l => l.User)
            .Include(l => l.Branch);

        if (!string.IsNullOrWhiteSpace(status))
        {
            switch (status.ToLowerInvariant())
            {
                case "active":
                    query = query.Where(l => !l.IsReturned && l.DueAt >= DateTime.UtcNow);
                    break;
                case "overdue":
                    query = query.Where(l => !l.IsReturned && l.DueAt < DateTime.UtcNow);
                    break;
                case "returned":
                    query = query.Where(l => l.IsReturned);
                    break;
            }
        }

        return await query
            .OrderByDescending(l => l.BorrowedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Loan>> GetOverdueLoansAsync()
    {
        return await _context.Loans
            .Where(l => !l.IsReturned && l.DueAt < DateTime.UtcNow)
            .Include(l => l.Book)
            .Include(l => l.User)
            .Include(l => l.Branch)
            .ToListAsync();
    }

    public async Task<decimal> CalculateFineAsync(int loanId)
    {
        var loan = await _context.Loans.FindAsync(loanId);
        if (loan == null)
        {
            return 0;
        }

        if (loan.IsReturned)
        {
            return loan.FineAmount;
        }

        if (DateTime.UtcNow <= loan.DueAt)
        {
            return 0;
        }

        var daysOverdue = (int)Math.Ceiling((DateTime.UtcNow - loan.DueAt).TotalDays);
        if (daysOverdue < 1) daysOverdue = 1;
        return daysOverdue * DailyFineAmount;
    }

    public async Task<bool> MarkFinePaidAsync(int loanId)
    {
        var loan = await _context.Loans.FindAsync(loanId);
        if (loan == null)
        {
            return false;
        }

        loan.FinePaid = true;
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Fine marked as paid for loan {loanId}");

        return true;
    }

    public async Task<int> GetActiveLoansCountAsync(int userId)
    {
        return await _context.Loans
            .Where(l => l.UserId == userId && !l.IsReturned)
            .CountAsync();
    }
}
