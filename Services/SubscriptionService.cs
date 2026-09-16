using BookShelf.Data;
using BookShelf.Models;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Services;

public interface ISubscriptionService
{
    Task<SubscriptionPlan> CreateSubscriptionPlanAsync(string name, decimal price, int durationInDays, int maxActiveLoans, decimal discountPercent);
    Task<UserSubscription> SubscribeUserAsync(int userId, int planId);
    Task<bool> CancelSubscriptionAsync(int subscriptionId);
    Task<UserSubscription?> GetActiveSubscriptionAsync(int userId);
    Task<IEnumerable<SubscriptionPlan>> GetActiveSubscriptionPlansAsync();
    Task<bool> CheckAndExpireSubscriptionsAsync();
}

public class SubscriptionService : ISubscriptionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(AppDbContext context, ILogger<SubscriptionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SubscriptionPlan> CreateSubscriptionPlanAsync(string name, decimal price, int durationInDays, int maxActiveLoans, decimal discountPercent)
    {
        var plan = new SubscriptionPlan
        {
            Name = name,
            Price = price,
            DurationInDays = durationInDays,
            MaxActiveLoans = maxActiveLoans,
            DiscountPercentOnPurchases = discountPercent,
            IsActive = true
        };

        _context.SubscriptionPlans.Add(plan);
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Subscription plan '{name}' created");

        return plan;
    }

    public async Task<UserSubscription> SubscribeUserAsync(int userId, int planId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        var plan = await _context.SubscriptionPlans.FindAsync(planId);
        if (plan == null)
        {
            throw new InvalidOperationException("Subscription plan not found.");
        }

        if (!plan.IsActive)
        {
            throw new InvalidOperationException("Subscription plan is not active.");
        }

        // Cancel existing subscription if any
        var existingSubscription = await _context.UserSubscriptions
            .FirstOrDefaultAsync(us => us.UserId == userId && us.Status == "Active");

        if (existingSubscription != null)
        {
            existingSubscription.Status = "Cancelled";
        }

        var startDate = DateTime.UtcNow;
        var endDate = startDate.AddDays(plan.DurationInDays);

        var subscription = new UserSubscription
        {
            UserId = userId,
            PlanId = planId,
            StartDate = startDate,
            EndDate = endDate,
            Status = "Active",
            AutoRenew = true
        };

        _context.UserSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();
        _logger.LogInformation($"User {userId} subscribed to plan {planId}");

        return subscription;
    }

    public async Task<bool> CancelSubscriptionAsync(int subscriptionId)
    {
        var subscription = await _context.UserSubscriptions.FindAsync(subscriptionId);
        if (subscription == null)
        {
            return false;
        }

        subscription.Status = "Cancelled";
        subscription.AutoRenew = false;
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Subscription {subscriptionId} cancelled");

        return true;
    }

    public async Task<UserSubscription?> GetActiveSubscriptionAsync(int userId)
    {
        return await _context.UserSubscriptions
            .Where(us => us.UserId == userId && us.Status == "Active")
            .Include(us => us.Plan)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<SubscriptionPlan>> GetActiveSubscriptionPlansAsync()
    {
        return await _context.SubscriptionPlans
            .Where(sp => sp.IsActive)
            .ToListAsync();
    }

    public async Task<bool> CheckAndExpireSubscriptionsAsync()
    {
        var expiredSubscriptions = await _context.UserSubscriptions
            .Where(us => us.Status == "Active" && us.EndDate < DateTime.UtcNow)
            .ToListAsync();

        if (expiredSubscriptions.Count == 0)
        {
            return true;
        }

        foreach (var subscription in expiredSubscriptions)
        {
            if (subscription.AutoRenew)
            {
                subscription.Status = "Active";
                subscription.StartDate = DateTime.UtcNow;
                subscription.EndDate = DateTime.UtcNow.AddDays(subscription.Plan.DurationInDays);
            }
            else
            {
                subscription.Status = "Expired";
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation($"Checked and updated {expiredSubscriptions.Count} subscriptions");

        return true;
    }
}
