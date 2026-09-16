using BookShelf.Data;
using BookShelf.Models;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Services;

public interface IBookService
{
    Task<Book> CreateBookAsync(int authorProfileId, string title, string description, string isbn, string coverImageUrl, decimal? price, bool isForSale, bool isForLoan, int stockForSale, int stockForLoan);
    Task<bool> ApproveBookAsync(int bookId, int adminId);
    Task<bool> RejectBookAsync(int bookId, int adminId);
    Task<bool> DelistBookAsync(int bookId);
    Task<Book?> GetBookByIdAsync(int bookId);
    Task<IEnumerable<Book>> GetApprovedBooksAsync();
    Task<IEnumerable<Book>> GetPendingBooksAsync();
    Task<IEnumerable<Book>> GetAuthorBooksAsync(int authorProfileId);
    Task<bool> UpdateStockAsync(int bookId, int stockForSale, int stockForLoan);
}

public class BookService : IBookService
{
    private readonly AppDbContext _context;
    private readonly ILogger<BookService> _logger;

    public BookService(AppDbContext context, ILogger<BookService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Book> CreateBookAsync(int authorProfileId, string title, string description, string isbn, string coverImageUrl, decimal? price, bool isForSale, bool isForLoan, int stockForSale, int stockForLoan)
    {
        var authorProfile = await _context.AuthorProfiles.FindAsync(authorProfileId);
        if (authorProfile == null)
        {
            throw new InvalidOperationException("Author profile not found.");
        }

        if (authorProfile.Status != "Approved")
        {
            throw new InvalidOperationException("Only approved authors can submit books.");
        }

        if (isForSale && price <= 0)
        {
            throw new InvalidOperationException("Price must be greater than 0 for sale books.");
        }

        var book = new Book
        {
            AuthorProfileId = authorProfileId,
            Title = title,
            Description = description,
            ISBN = isbn,
            CoverImageUrl = coverImageUrl,
            Price = price,
            IsForSale = isForSale,
            IsForLoan = isForLoan,
            StockForSale = isForSale ? stockForSale : 0,
            StockForLoan = isForLoan ? stockForLoan : 0,
            Status = "Pending"
        };

        _context.Books.Add(book);
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Book '{title}' submitted by author {authorProfileId}");

        return book;
    }

    public async Task<bool> ApproveBookAsync(int bookId, int adminId)
    {
        var book = await _context.Books.FindAsync(bookId);
        if (book == null)
        {
            return false;
        }

        book.Status = "Approved";
        book.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Book {bookId} approved by admin {adminId}");

        return true;
    }

    public async Task<bool> RejectBookAsync(int bookId, int adminId)
    {
        var book = await _context.Books.FindAsync(bookId);
        if (book == null)
        {
            return false;
        }

        book.Status = "Rejected";
        book.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Book {bookId} rejected by admin {adminId}");

        return true;
    }

    public async Task<bool> DelistBookAsync(int bookId)
    {
        var book = await _context.Books.FindAsync(bookId);
        if (book == null)
        {
            return false;
        }

        book.Status = "Delisted";
        book.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Book {bookId} delisted");

        return true;
    }

    public async Task<Book?> GetBookByIdAsync(int bookId)
    {
        return await _context.Books
            .Include(b => b.AuthorProfile)
            .FirstOrDefaultAsync(b => b.Id == bookId);
    }

    public async Task<IEnumerable<Book>> GetApprovedBooksAsync()
    {
        return await _context.Books
            .Where(b => b.Status == "Approved")
            .Include(b => b.AuthorProfile)
            .ToListAsync();
    }

    public async Task<IEnumerable<Book>> GetPendingBooksAsync()
    {
        return await _context.Books
            .Where(b => b.Status == "Pending")
            .Include(b => b.AuthorProfile)
            .ToListAsync();
    }

    public async Task<IEnumerable<Book>> GetAuthorBooksAsync(int authorProfileId)
    {
        return await _context.Books
            .Where(b => b.AuthorProfileId == authorProfileId)
            .ToListAsync();
    }

    public async Task<bool> UpdateStockAsync(int bookId, int stockForSale, int stockForLoan)
    {
        var book = await _context.Books.FindAsync(bookId);
        if (book == null)
        {
            return false;
        }

        book.StockForSale = stockForSale;
        book.StockForLoan = stockForLoan;
        book.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Stock updated for book {bookId}");

        return true;
    }
}

