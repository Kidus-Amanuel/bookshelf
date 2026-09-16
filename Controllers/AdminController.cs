using System.Security.Claims;
using BookShelf.Data;
using BookShelf.Models;
using BookShelf.Services;
using BookShelf.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Controllers;

[Authorize]
[Route("admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _context;
    private readonly IAuthService _authService;
    private readonly IBookService _bookService;
    private readonly IOrderService _orderService;
    private readonly ILoanService _loanService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        AppDbContext context,
        IAuthService authService,
        IBookService bookService,
        IOrderService orderService,
        ILoanService loanService,
        ILogger<AdminController> logger)
    {
        _context = context;
        _authService = authService;
        _bookService = bookService;
        _orderService = orderService;
        _loanService = loanService;
        _logger = logger;
    }

    // ========== ADMIN DASHBOARD ==========

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var stats = new
        {
            PendingAuthorApplications = await _context.AuthorProfiles.CountAsync(ap => ap.Status == "Pending"),
            PendingBooks = await _context.Books.CountAsync(b => b.Status == "Pending"),
            TotalUsers = await _context.Users.CountAsync(),
            TotalOrders = await _context.Orders.CountAsync(),
            OverdueLoans = await _context.Loans.CountAsync(l => !l.IsReturned && l.DueAt < DateTime.UtcNow)
        };

        return View(stats);
    }

    // ========== AUTHOR APPLICATION MANAGEMENT ==========

    // List pending author applications
    [HttpGet("AuthorApplications")]
    public async Task<IActionResult> AuthorApplications()
    {
        var applications = await _context.AuthorProfiles
            .Include(ap => ap.User)
            .OrderByDescending(ap => ap.AppliedAt)
            .ToListAsync();

        return View("~/Views/Home/Admin/Authorapplications.cshtml", applications);
    }

    // Single application detail / review form
    [HttpGet("AuthorApplicationDetails/{id:int}")]
    public async Task<IActionResult> AuthorApplicationDetails(int id)
    {
        var application = await _context.AuthorProfiles
            .Include(ap => ap.User)
            .Include(ap => ap.ApprovedByAdmin)
            .Include(ap => ap.Books)
            .FirstOrDefaultAsync(ap => ap.Id == id);

        if (application == null)
        {
            return NotFound();
        }

        return View("~/Views/Home/Admin/Authorapplicationsform.cshtml", application);
    }

    // Approve author application
    [HttpPost("ApproveAuthor/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveAuthor(int id)
    {
        try
        {
            var adminId = CurrentUserId() ?? 0;
            if (adminId == 0)
            {
                return Unauthorized();
            }

            var success = await _authService.ApproveAuthorApplicationAsync(id, adminId);
            if (!success)
            {
                return NotFound();
            }

            _logger.LogInformation("Author application {Id} approved by admin {AdminId}", id, adminId);
            TempData["Success"] = "Author application approved.";
            return RedirectToAction(nameof(AuthorApplications));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving author {Id}", id);
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(AuthorApplications));
        }
    }

    // Reject author application
    [HttpPost("RejectAuthor/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectAuthor(int id)
    {
        try
        {
            var adminId = CurrentUserId() ?? 0;
            if (adminId == 0)
            {
                return Unauthorized();
            }

            var success = await _authService.RejectAuthorApplicationAsync(id, adminId);
            if (!success)
            {
                return NotFound();
            }

            _logger.LogInformation("Author application {Id} rejected by admin {AdminId}", id, adminId);
            TempData["Success"] = "Author application rejected.";
            return RedirectToAction(nameof(AuthorApplications));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting author {Id}", id);
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(AuthorApplications));
        }
    }

    // ========== BOOK APPROVALS ==========

    // List books awaiting approval (with optional status filter)
    [HttpGet("PendingBooks")]
    public async Task<IActionResult> PendingBooks(string? status = null)
    {
        var query = _context.Books
            .Include(b => b.AuthorProfile)
                .ThenInclude(ap => ap!.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            // Treat "PendingBooks" default as the pending queue
            var filter = status.Equals("pending", StringComparison.OrdinalIgnoreCase) ? "Pending" : status;
            query = query.Where(b => b.Status == filter);
        }
        else
        {
            query = query.Where(b => b.Status == "Pending");
        }

        var books = await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        ViewData["Filter"] = status ?? "Pending";
        ViewData["AllBooks"] = await _context.Books
            .CountAsync();
        ViewData["PendingCount"] = await _bookService.GetPendingBooksAsync() is var pending ? (pending as IEnumerable<Book>)?.Count() ?? 0 : 0;
        ViewData["ApprovedCount"] = await _context.Books.CountAsync(b => b.Status == "Approved");
        ViewData["RejectedCount"] = await _context.Books.CountAsync(b => b.Status == "Rejected");

        return View("~/Views/Home/Admin/Bookapprovals.cshtml", books);
    }

    // Approve a book
    [HttpPost("ApproveBook/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveBook(int id)
    {
        try
        {
            var adminId = CurrentUserId() ?? 0;
            if (adminId == 0) return Unauthorized();

            var success = await _bookService.ApproveBookAsync(id, adminId);
            if (!success) return NotFound();

            _logger.LogInformation("Book {Id} approved by admin {AdminId}", id, adminId);
            TempData["Success"] = "Book approved and published to the catalog.";
            return RedirectToAction(nameof(PendingBooks));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving book {Id}", id);
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(PendingBooks));
        }
    }

    // Reject a book
    [HttpPost("RejectBook/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectBook(int id)
    {
        try
        {
            var adminId = CurrentUserId() ?? 0;
            if (adminId == 0) return Unauthorized();

            var success = await _bookService.RejectBookAsync(id, adminId);
            if (!success) return NotFound();

            _logger.LogInformation("Book {Id} rejected by admin {AdminId}", id, adminId);
            TempData["Success"] = "Book rejected.";
            return RedirectToAction(nameof(PendingBooks));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting book {Id}", id);
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(PendingBooks));
        }
    }

    // ========== ADMIN INVITATION ==========

    [HttpGet("InviteAdmin")]
    public IActionResult InviteAdmin() => View();

    [HttpPost("InviteAdmin")]
    public async Task<IActionResult> InviteAdmin(InviteAdminRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                TempData["Error"] = "Email is required.";
                return RedirectToAction(nameof(InviteAdmin));
            }

            var adminId = CurrentUserId() ?? 0;
            if (adminId == 0) return Unauthorized();

            var invite = await _authService.CreateAdminInviteAsync(request.Email, adminId);
            TempData["Success"] = $"Invitation created for {request.Email}. Share the signup link with the new admin.";

            _logger.LogInformation("Admin invite created for {Email}", request.Email);
            return RedirectToAction(nameof(InviteAdmin));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inviting admin");
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(InviteAdmin));
        }
    }

    // ========== USER MANAGEMENT ==========

    // All members / users
    [HttpGet("Users")]
    public async Task<IActionResult> Users(string? role = null, string? q = null)
    {
        var query = _context.Users
            .Include(u => u.Role)
            .Include(u => u.AuthorProfile)
            .Include(u => u.StaffProfile)
                .ThenInclude(sp => sp!.Branch)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role.Name == role);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim().ToLower();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(needle) ||
                u.Email.ToLower().Contains(needle));
        }

        var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();

        var membersVm = new MembersViewModel
        {
            Users = users,
            Roles = await _context.Roles.OrderBy(r => r.Name).ToListAsync(),
            FilterRole = role,
            SearchTerm = q,
            TotalCount = await _context.Users.CountAsync(),
            CustomerCount = await _context.Users.CountAsync(u => u.Role.Name == "Customer"),
            AdminCount = await _context.Users.CountAsync(u => u.Role.Name == "Admin"),
            StaffCount = await _context.Users.CountAsync(u => u.Role.Name == "Staff"),
            ApprovedAuthorCount = await _context.AuthorProfiles.CountAsync(ap => ap.Status == "Approved"),
            PendingAuthorCount = await _context.AuthorProfiles.CountAsync(ap => ap.Status == "Pending")
        };

        return View("~/Views/Home/Admin/Members.cshtml", membersVm);
    }

    // ========== BRANCH MANAGEMENT ==========

    [HttpGet("Branches")]
    public async Task<IActionResult> Branches()
    {
        var branches = await _context.Branches
            .Include(b => b.Staff)
            .ToListAsync();
        return View("~/Views/Admin/Branches.cshtml", branches);
    }

    [HttpGet("CreateBranch")]
    public IActionResult CreateBranch() => View("~/Views/Admin/CreateBranch.cshtml");

    [HttpPost("CreateBranch")]
    public async Task<IActionResult> CreateBranch(CreateBranchRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                TempData["Error"] = "Branch name is required.";
                return RedirectToAction(nameof(CreateBranch));
            }

            var branch = new Branch
            {
                Name = request.Name,
                Address = request.Address,
                Phone = request.Phone
            };

            _context.Branches.Add(branch);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Branch '{branch.Name}' created.";
            _logger.LogInformation("Branch '{Name}' created", request.Name);
            return RedirectToAction(nameof(Branches));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating branch");
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(CreateBranch));
        }
    }

    [HttpPost("AssignStaff")]
    public async Task<IActionResult> AssignStaff([FromBody] AssignStaffRequest request)
    {
        try
        {
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null) return NotFound("User not found.");

            if (user.StaffProfile != null)
                return BadRequest("User already has a staff profile.");

            var branch = await _context.Branches.FindAsync(request.BranchId);
            if (branch == null) return NotFound("Branch not found.");

            var staffProfile = new StaffProfile
            {
                UserId = request.UserId,
                BranchId = request.BranchId,
                IsActive = true
            };

            _context.StaffProfiles.Add(staffProfile);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} assigned to branch {BranchId}", request.UserId, request.BranchId);
            return Ok(new { message = "Staff assigned successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning staff");
            return BadRequest(ex.Message);
        }
    }

    // ========== REPORTS ==========

    [HttpGet("OrdersReport")]
    public async Task<IActionResult> OrdersReport()
    {
        var orders = (await _orderService.GetAllOrdersAsync()).ToList();
        var totalRevenue = orders
            .Where(o => o.Status == "Paid" || o.Status == "Shipped" || o.Status == "Delivered")
            .Sum(o => o.TotalAmount);
        ViewData["TotalRevenue"] = totalRevenue;
        ViewData["PendingCount"] = orders.Count(o => o.Status == "Pending");
        ViewData["PaidCount"] = orders.Count(o => o.Status == "Paid");
        ViewData["ShippedCount"] = orders.Count(o => o.Status == "Shipped");
        ViewData["DeliveredCount"] = orders.Count(o => o.Status == "Delivered");
        ViewData["CancelledCount"] = orders.Count(o => o.Status == "Cancelled");
        return View("~/Views/Admin/OrdersReport.cshtml", orders);
    }

    [HttpGet("LoansReport")]
    public async Task<IActionResult> LoansReport()
    {
        var loans = (await _loanService.GetAllLoansAsync()).ToList();
        ViewData["ActiveCount"] = loans.Count(l => !l.IsReturned && l.DueAt >= DateTime.UtcNow);
        ViewData["OverdueCount"] = loans.Count(l => !l.IsReturned && l.DueAt < DateTime.UtcNow);
        ViewData["ReturnedCount"] = loans.Count(l => l.IsReturned);
        ViewData["OutstandingFines"] = loans.Where(l => !l.FinePaid).Sum(l => l.FineAmount);
        return View("~/Views/Admin/LoansReport.cshtml", loans);
    }

    // ========== HELPERS ==========

    private int? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : (int?)null;
    }
}

public class InviteAdminRequest
{
    public string Email { get; set; } = string.Empty;
}

public class CreateBranchRequest
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public class AssignStaffRequest
{
    public int UserId { get; set; }
    public int BranchId { get; set; }
}