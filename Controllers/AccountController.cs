using System.Security.Claims;
using BookShelf.Data;
using BookShelf.Models;
using BookShelf.Services;
using BookShelf.ViewModels.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _context;
    private readonly PasswordHasher<User> _passwordHasher;
    private readonly IAuthService _authService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        AppDbContext context,
        IAuthService authService,
        ISubscriptionService subscriptionService,
        ILogger<AccountController> logger)
    {
        _context = context;
        _passwordHasher = new PasswordHasher<User>();
        _authService = authService;
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _context.Users
            .Include(x => x.Role)
            .Include(x => x.AuthorProfile)
            .FirstOrDefaultAsync(x => x.Email == model.Email);

        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        var passwordResult = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            model.Password
        );

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.Name)
        };

        if (user.AuthorProfile?.Status == "Approved")
        {
            claims.Add(new Claim("AuthorStatus", "Approved"));
        }

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties
        );

        _logger.LogInformation($"User {user.Email} signed in (role={user.Role.Name})");

        if (user.Role.Name == "Customer")
        {
            if (user.AuthorProfile?.Status == "Approved")
            {
                return RedirectToAction(nameof(HomeController.Author), "Home");
            }

            var activeSub = await _subscriptionService.GetActiveSubscriptionAsync(user.Id);
            if (activeSub == null)
            {
                return RedirectToAction("Customer", "Onboarding");
            }

            return RedirectToAction(nameof(HomeController.Customer), "Home");
        }

        if (user.Role.Name == "Admin" || user.Role.Name == "Staff")
        {
            return RedirectToAction(nameof(HomeController.Admin), "Home");
        }

        return RedirectToAction(nameof(HomeController.Customer), "Home");
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.Password != model.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");
            return View(model);
        }

        try
        {
            // Check if user already exists
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError(nameof(model.Email), "Email is already registered.");
                return View(model);
            }

            // Get customer role
            var customerRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Customer");
            if (customerRole == null)
            {
                throw new InvalidOperationException("Customer role not found.");
            }

            // Hash password
            var user = new User
            {
                FullName = model.FullName,
                Email = model.Email,
                RoleId = customerRole.Id
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"New user registered: {model.Email}");

            // Automatically sign in the registered user
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, customerRole.Name)
            };
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) }
            );

            return RedirectToAction("Customer", "Onboarding");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error registering user: {ex.Message}");
            ModelState.AddModelError(string.Empty, "Error registering user. Please try again.");
            return View(model);
        }
    }

    // Admin signup via invite link
    [HttpGet("admin-signup")]
    public async Task<IActionResult> AdminSignup(string email, string token)
    {
        try
        {
            var isValid = await _authService.IsValidInviteAsync(email, token);
            if (!isValid)
            {
                return BadRequest("Invalid or expired invitation.");
            }

            var model = new AdminSignupViewModel
            {
                Email = email,
                Token = token
            };

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading admin signup: {ex.Message}");
            return BadRequest("Error loading invitation.");
        }
    }

    [HttpPost("admin-signup")]
    public async Task<IActionResult> AdminSignup(AdminSignupViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.Password != model.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");
            return View(model);
        }

        try
        {
            var hasher = new PasswordHasher<User>();
            var passwordHash = hasher.HashPassword(new User(), model.Password);

            var user = await _authService.CompleteAdminSignupAsync(
                model.Email,
                model.Token,
                model.FullName,
                passwordHash
            );

            _logger.LogInformation($"Admin user created: {model.Email}");
            return RedirectToAction(nameof(Login));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error completing admin signup: {ex.Message}");
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    // User applies to become an author
    [HttpGet("become-author")]
    public IActionResult BecomeAuthor()
    {
        // TODO: Check if user is authenticated and already not an author
        return View();
    }

    [HttpPost("become-author")]
    public async Task<IActionResult> BecomeAuthor(BecomeAuthorViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var userId = CurrentUserId() ?? 1;
            
            var profile = await _authService.ApplyAsAuthorAsync(userId, model.PenName, model.Bio);
            _logger.LogInformation($"User {userId} applied to become an author");
            
            return RedirectToAction(nameof(AuthorApplicationPending));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error applying as author: {ex.Message}");
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet("author-application-pending")]
    public IActionResult AuthorApplicationPending()
    {
        return View();
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    public IActionResult ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        return RedirectToAction(nameof(EmailConfirmation), new { email = model.Email });
    }

    [HttpGet]
    public IActionResult EmailConfirmation(string? email, bool isConfirmed = false)
    {
        var model = new EmailConfirmationViewModel
        {
            Email = email ?? string.Empty,
            IsConfirmed = isConfirmed
        };

        return View(model);
    }

    [HttpPost]
    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("User signed out.");
        return RedirectToAction(nameof(Login));
    }

    private int? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : (int?)null;
    }
}

