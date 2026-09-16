using BookShelf.Data;
using BookShelf.Models;
using BookShelf.Services;
using BookShelf.ViewModels.Books;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Controllers;

public class BooksController : Controller
{
    private readonly AppDbContext _context;
    private readonly IBookService _bookService;
    private readonly IAuthService _authService;
    private readonly ILogger<BooksController> _logger;

    public BooksController(AppDbContext context, IBookService bookService, IAuthService authService, ILogger<BooksController> logger)
    {
        _context = context;
        _bookService = bookService;
        _authService = authService;
        _logger = logger;
    }

    // Public catalog - shows only approved books
    public async Task<IActionResult> Index()
    {
        var books = await _bookService.GetApprovedBooksAsync();
        return View(books);
    }

    public async Task<IActionResult> Details(int id)
    {
        var book = await _bookService.GetBookByIdAsync(id);
        if (book == null)
        {
            return NotFound();
        }
        return View(book);
    }

    // Author submits a new book
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        // Check if user is an approved author
        // TODO: Add authentication and authorization
        var authorProfile = await _authService.GetAuthorProfileAsync(1); // Placeholder: get from auth
        if (authorProfile == null || authorProfile.Status != "Approved")
        {
            return Forbid("Only approved authors can create books.");
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(BookViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            // TODO: Get current user from authentication
            var authorProfile = await _authService.GetAuthorProfileAsync(1);
            if (authorProfile == null || authorProfile.Status != "Approved")
            {
                return Forbid();
            }

            var book = await _bookService.CreateBookAsync(
                authorProfile.Id,
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

            _logger.LogInformation($"Book '{book.Title}' submitted for approval");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating book: {ex.Message}");
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    // Author: view their own books
    [HttpGet("MyBooks")]
    public async Task<IActionResult> MyBooks()
    {
        // TODO: Get current user from auth
        var authorProfile = await _authService.GetAuthorProfileAsync(1);
        if (authorProfile == null)
        {
            return Forbid();
        }

        var books = await _bookService.GetAuthorBooksAsync(authorProfile.Id);
        return View(books);
    }
}
