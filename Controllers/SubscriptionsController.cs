using System.Security.Claims;
using BookShelf.Data;
using BookShelf.Models;
using BookShelf.Services;
using BookShelf.ViewModels.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Controllers;

[Route("Subscription")]
[Route("Subscriptions")]
public class SubscriptionsController : Controller
{
    private readonly AppDbContext _context;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILoanService _loanService;
    private readonly ILogger<SubscriptionsController> _logger;

    public SubscriptionsController(
        AppDbContext context,
        ISubscriptionService subscriptionService,
        ILoanService loanService,
        ILogger<SubscriptionsController> logger)
    {
        _context = context;
        _subscriptionService = subscriptionService;
        _loanService = loanService;
        _logger = logger;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var userId = CurrentUserId();
        if (userId == null) return Challenge();

        var current = await _subscriptionService.GetActiveSubscriptionAsync(userId.Value);
        var plans = await _subscriptionService.GetActiveSubscriptionPlansAsync();
        var activeLoans = await _loanService.GetActiveLoansCountAsync(userId.Value);
        var daysRemaining = current != null
            ? Math.Max(0, (int)Math.Ceiling((current.EndDate - DateTime.UtcNow).TotalDays))
            : 0;

        var model = new SubscriptionsViewModel
        {
            Current = current,
            AvailablePlans = plans,
            ActiveLoans = activeLoans,
            DaysRemaining = daysRemaining
        };

        return View("~/Views/Subscription/Index.cshtml", model);
    }

    // Start Free Trial
    [HttpPost("start-trial")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartTrial()
    {
        var userId = CurrentUserId();
        if (userId == null) return Challenge();

        var plans = await _subscriptionService.GetActiveSubscriptionPlansAsync();
        var trialPlan = plans.FirstOrDefault(p => p.Price == 0 || p.Name.Contains("Trial", StringComparison.OrdinalIgnoreCase));

        if (trialPlan == null)
        {
            TempData["Error"] = "Free trial plan is currently unavailable.";
            return RedirectToAction(nameof(Index));
        }

        await _subscriptionService.SubscribeUserAsync(userId.Value, trialPlan.Id);
        TempData["Success"] = "Your free trial is now active! Enjoy borrowing books.";
        return RedirectToAction("Customer", "Home");
    }

    // Checkout redirect for chosen plan
    [HttpPost("checkout")]
    public IActionResult Checkout(int planId)
    {
        return RedirectToAction("Checkout", "Orders", new { planId });
    }

    // Toggle auto-renew
    [HttpPost("toggle-autorenew")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAutoRenew()
    {
        var userId = CurrentUserId();
        if (userId == null) return Challenge();

        var sub = await _subscriptionService.GetActiveSubscriptionAsync(userId.Value);
        if (sub != null)
        {
            sub.AutoRenew = !sub.AutoRenew;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Auto-renew is now {(sub.AutoRenew ? "enabled" : "disabled")}.";
        }

        return RedirectToAction(nameof(Index));
    }

    // Cancel current subscription from Index
    [HttpPost("cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel()
    {
        var userId = CurrentUserId();
        if (userId == null) return Challenge();

        var sub = await _subscriptionService.GetActiveSubscriptionAsync(userId.Value);
        if (sub != null)
        {
            await _subscriptionService.CancelSubscriptionAsync(sub.Id);
            TempData["Success"] = "Your subscription has been cancelled.";
        }

        return RedirectToAction(nameof(Index));
    }

    // Browse available subscription plans
    [HttpGet("browse")]
    public async Task<IActionResult> Browse()
    {
        var plans = await _subscriptionService.GetActiveSubscriptionPlansAsync();
        return View(plans);
    }

    // User's current subscription
    [HttpGet("my-subscription")]
    public async Task<IActionResult> MySubscription()
    {
        var userId = CurrentUserId() ?? 1;
        var subscription = await _subscriptionService.GetActiveSubscriptionAsync(userId);
        return View(subscription);
    }

    private int? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : (int?)null;
    }

    // Subscribe to a plan
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
    {
        try
        {
            // TODO: Get current user from auth
            var userId = 1; // Placeholder
            
            var plan = await _context.SubscriptionPlans.FindAsync(request.PlanId);
            if (plan == null || !plan.IsActive)
            {
                return NotFound("Subscription plan not found.");
            }

            var subscription = await _subscriptionService.SubscribeUserAsync(userId, request.PlanId);

            // Create payment record
            var payment = new Payment
            {
                SubscriptionId = subscription.Id,
                Amount = plan.Price,
                Status = "Completed",
                PaymentMethod = "Card",
                PaidAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"User {userId} subscribed to plan {request.PlanId}");
            return Ok(new { message = "Successfully subscribed", subscriptionId = subscription.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error subscribing: {ex.Message}");
            return BadRequest(ex.Message);
        }
    }

    // Cancel subscription
    [HttpPost("cancel/{subscriptionId}")]
    public async Task<IActionResult> Cancel(int subscriptionId)
    {
        try
        {
            var subscription = await _context.UserSubscriptions.FindAsync(subscriptionId);
            if (subscription == null)
            {
                return NotFound();
            }

            // TODO: Verify user owns this subscription

            var success = await _subscriptionService.CancelSubscriptionAsync(subscriptionId);
            if (!success)
            {
                return BadRequest("Failed to cancel subscription");
            }

            _logger.LogInformation($"Subscription {subscriptionId} cancelled");
            return Ok(new { message = "Subscription cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error cancelling subscription: {ex.Message}");
            return BadRequest(ex.Message);
        }
    }

    // Admin: create new subscription plan
    [HttpPost("admin/create-plan")]
    public async Task<IActionResult> CreatePlan([FromBody] CreatePlanRequest request)
    {
        try
        {
            // TODO: Add admin authorization
            var plan = await _subscriptionService.CreateSubscriptionPlanAsync(
                request.Name,
                request.Price,
                request.DurationInDays,
                request.MaxActiveLoans,
                request.DiscountPercent
            );

            _logger.LogInformation($"Subscription plan '{plan.Name}' created");
            return Ok(new { message = "Subscription plan created", planId = plan.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating plan: {ex.Message}");
            return BadRequest(ex.Message);
        }
    }

    // Admin: check and expire subscriptions (typically runs as background job)
    [HttpPost("admin/process-expiries")]
    public async Task<IActionResult> ProcessExpiries()
    {
        try
        {
            // TODO: Add admin authorization and consider making this a background job
            var success = await _subscriptionService.CheckAndExpireSubscriptionsAsync();
            if (!success)
            {
                return BadRequest("Failed to process expirations");
            }

            _logger.LogInformation("Subscription expirations processed");
            return Ok(new { message = "Subscription expirations processed" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing expiries: {ex.Message}");
            return BadRequest(ex.Message);
        }
    }
}

public class SubscribeRequest
{
    public int PlanId { get; set; }
}

public class CreatePlanRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationInDays { get; set; }
    public int MaxActiveLoans { get; set; }
    public decimal DiscountPercent { get; set; }
}
