using BookShelf.Data;
using BookShelf.Models;
using BookShelf.ViewModels.Loans;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Controllers;

public class LoansController : Controller
{
    private readonly AppDbContext _context;

    public LoansController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var loans = await _context.Loans
            .Include(l => l.Book)
            .Include(l => l.User)
            .ToListAsync();

        return View(loans);
    }

    public IActionResult Borrow()
    {
        return View();
    }
}
