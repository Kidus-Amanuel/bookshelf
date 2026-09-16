using System.Security.Claims;
using BookShelf.Data;
using BookShelf.Services;
using BookShelf.ViewModels.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Controllers;

[Authorize]
public class OnboardingController : Controller
{
    private readonly AppDbContext _context;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<OnboardingController> _logger;

    public OnboardingController(
        AppDbContext context,
        ISubscriptionService subscriptionService,
        ILogger<OnboardingController> logger)
    {
        _context = context;
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    [HttpGet("onboarding/customer")]
    [HttpGet("onboarding/customer/index")]
    public async Task<IActionResult> Customer()
    {
        var userId = CurrentUserId();
        if (userId == null) return Challenge();

        var user = await _context.Users.FindAsync(userId.Value);
        var plans = (await _subscriptionService.GetActiveSubscriptionPlansAsync()).ToList();

        var freeTrial = plans.FirstOrDefault(p => p.Price == 0 || p.Name.Contains("Trial", StringComparison.OrdinalIgnoreCase));
        var paidPlans = plans.Where(p => p != freeTrial).ToList();

        var model = new CustomerOnboardingViewModel
        {
            CustomerName = user?.FullName ?? User.Identity?.Name ?? "Reader",
            FreeTrialPlan = freeTrial,
            AvailablePlans = paidPlans
        };

        return View("~/Views/Onboarding/Customer/Index.cshtml", model);
    }

    private int? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : (int?)null;
    }
}
