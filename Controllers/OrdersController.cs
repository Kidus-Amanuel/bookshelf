using System.Security.Claims;
using BookShelf.Data;
using BookShelf.Models;
using BookShelf.Services;
using BookShelf.ViewModels.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Controllers;

[Authorize]
public class OrdersController : Controller
{
    private readonly AppDbContext _context;
    private readonly IOrderService _orderService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        AppDbContext context,
        IOrderService orderService,
        ISubscriptionService subscriptionService,
        ILogger<OrdersController> logger)
    {
        _context = context;
        _orderService = orderService;
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    // Admin: every order across the system
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index(string? status = null, string? channel = null)
    {
        var orders = await _orderService.GetAllOrdersAsync(status, channel);
        ViewBag.CurrentStatus = status ?? "all";
        ViewBag.CurrentChannel = channel ?? "all";
        return View(orders);
    }

    // Customer: my orders
    [HttpGet("my-orders")]
    public async Task<IActionResult> MyOrders()
    {
        var userId = CurrentUserId();
        if (userId == null) return Challenge();

        var orders = await _orderService.GetUserOrdersAsync(userId.Value);
        return View(orders);
    }

    // Order details (visible to owner or admin)
    [HttpGet("details/{id:int}")]
    public async Task<IActionResult> Details([FromRoute] int id)
    {
        var userId = CurrentUserId();
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null)
        {
            return NotFound();
        }
        if (userId != null && order.UserId != userId.Value && !User.IsInRole("Admin"))
        {
            return Forbid();
        }
        return View(order);
    }

    // Buy-now: from Books/Details "Buy now" button. Creates an order + adds the item + goes to cart.
    [HttpGet("create/{bookId:int}")]
    public async Task<IActionResult> Create([FromRoute] int bookId)
    {
        var userId = CurrentUserId();
        if (userId == null) return Challenge();

        try
        {
            var order = await _orderService.CreateOrderForBookAsync(userId.Value, bookId, quantity: 1);
            return RedirectToAction(nameof(Cart), new { id = order.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating order for book {bookId}: {ex.Message}");
            TempData["Error"] = ex.Message;
            return RedirectToAction("Details", "Books", new { id = bookId });
        }
    }

    // Cart (GET)
    [HttpGet("cart/{id:int}")]
    public async Task<IActionResult> Cart([FromRoute] int id)
    {
        var userId = CurrentUserId();
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null) return NotFound();
        if (userId != null && order.UserId != userId.Value && !User.IsInRole("Admin"))
        {
            return Forbid();
        }
        return View(order);
    }

    // Update item quantity in cart
    [HttpPost("cart/{orderId:int}/update-item")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCartItem([FromRoute] int orderId, int itemId, int quantity)
    {
        try
        {
            var ok = await _orderService.UpdateOrderItemQuantityAsync(orderId, itemId, quantity);
            if (!ok) return NotFound();
            return RedirectToAction(nameof(Cart), new { id = orderId });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating cart item: {ex.Message}");
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Cart), new { id = orderId });
        }
    }

    // Remove an item from cart
    [HttpPost("cart/{orderId:int}/remove-item/{itemId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCartItem([FromRoute] int orderId, [FromRoute] int itemId)
    {
        var ok = await _orderService.RemoveOrderItemAsync(orderId, itemId);
        if (!ok) return NotFound();
        return RedirectToAction(nameof(Cart), new { id = orderId });
    }

    // Checkout (GET) — review page for order or subscription
    [HttpGet("checkout/{id:int?}")]
    [HttpGet("checkout")]
    public async Task<IActionResult> Checkout([FromRoute] int? id, [FromQuery] int? planId)
    {
        var userId = CurrentUserId();
        if (userId == null) return Challenge();

        // Case 1: Subscription plan checkout
        if (planId.HasValue)
        {
            var plan = await _context.SubscriptionPlans.FindAsync(planId.Value);
            if (plan == null) return NotFound("Subscription plan not found.");

            var subVm = new CheckoutViewModel
            {
                PlanId = plan.Id,
                Title = $"Membership: {plan.Name}",
                Subtitle = "Review your chosen plan & pay",
                Items =
                [
                    new CheckoutItemViewModel
                    {
                        Title = $"{plan.Name} Membership ({plan.DurationInDays} days access, up to {plan.MaxActiveLoans} concurrent loans)",
                        Quantity = 1,
                        UnitPrice = plan.Price
                    }
                ],
                Subtotal = plan.Price,
                DiscountPercent = 0,
                TotalAmount = plan.Price,
                Channel = "Online",
                PaymentMethod = "Card"
            };

            return View(subVm);
        }

        // Case 2: Order checkout
        int targetOrderId = id ?? 0;
        if (targetOrderId == 0)
        {
            // Try to find pending cart for user
            var cart = await _orderService.GetOrCreateCartAsync(userId.Value);
            if (cart == null || !cart.OrderItems.Any())
            {
                return RedirectToAction(nameof(Cart), new { id = cart?.Id ?? 0 });
            }
            targetOrderId = cart.Id;
        }

        var order = await _orderService.GetOrderByIdAsync(targetOrderId);
        if (order == null) return NotFound();
        if (order.UserId != userId.Value && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var orderVm = new CheckoutViewModel
        {
            OrderId = order.Id,
            Title = $"Order #{order.Id}",
            Subtitle = "Review your items & pay",
            Items = order.OrderItems.Select(oi => new CheckoutItemViewModel
            {
                Title = oi.Book.Title,
                CoverImageUrl = oi.Book.CoverImageUrl,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice
            }).ToList(),
            Subtotal = order.Subtotal > 0 ? order.Subtotal : order.OrderItems.Sum(oi => oi.UnitPrice * oi.Quantity),
            DiscountPercent = order.DiscountPercent,
            TotalAmount = order.TotalAmount,
            Channel = order.Channel,
            BranchName = order.Branch?.Name,
            PaymentMethod = "Card"
        };

        return View(orderVm);
    }

    // Checkout (POST) — pay & finalize order or subscription
    [HttpPost("checkout/{id:int?}")]
    [HttpPost("checkout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(
        [FromRoute] int? id,
        [FromForm] int? orderId,
        [FromForm] int? planId,
        [FromForm] string? paymentMethod)
    {
        var userId = CurrentUserId();
        if (userId == null) return Challenge();

        try
        {
            // Case 1: Subscription Checkout Payment
            if (planId.HasValue)
            {
                var plan = await _context.SubscriptionPlans.FindAsync(planId.Value);
                if (plan == null) return NotFound("Subscription plan not found.");

                var subscription = await _subscriptionService.SubscribeUserAsync(userId.Value, planId.Value);

                var payment = new Payment
                {
                    SubscriptionId = subscription.Id,
                    Amount = plan.Price,
                    Status = "Completed",
                    PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "Card" : paymentMethod,
                    PaidAt = DateTime.UtcNow
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {userId.Value} subscribed to plan {plan.Name} (${plan.Price}) via {paymentMethod}");
                TempData["Success"] = $"Payment successful! Welcome to your {plan.Name} plan.";

                return RedirectToAction("Customer", "Home");
            }

            // Case 2: Order Checkout Payment
            int targetOrderId = orderId ?? id ?? 0;
            if (targetOrderId == 0)
            {
                return BadRequest("Invalid order.");
            }

            var order = await _orderService.GetOrderByIdAsync(targetOrderId);
            if (order == null) return NotFound();

            var success = await _orderService.ProcessCheckoutAsync(
                targetOrderId,
                order.TotalAmount,
                paymentMethod ?? "Card"
            );

            if (!success)
            {
                TempData["Error"] = "Checkout failed. Please try again.";
                return RedirectToAction(nameof(Checkout), new { id = targetOrderId });
            }

            _logger.LogInformation($"Order {targetOrderId} checked out via {paymentMethod}");
            return RedirectToAction(nameof(Confirmation), new { id = targetOrderId });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during checkout: {ex.Message}");
            TempData["Error"] = ex.Message;
            if (planId.HasValue)
            {
                return RedirectToAction(nameof(Checkout), new { planId });
            }
            return RedirectToAction(nameof(Checkout), new { id = orderId ?? id });
        }
    }

    // Confirmation page
    [HttpGet("confirmation/{id:int}")]
    public async Task<IActionResult> Confirmation([FromRoute] int id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null) return NotFound();
        return View(order);
    }

    // Admin: change order status (Pending / Paid / Shipped / Delivered / Cancelled)
    [Authorize(Roles = "Admin")]
    [HttpPost("update-status/{orderId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus([FromRoute] int orderId, string status)
    {
        var ok = await _orderService.UpdateOrderStatusAsync(orderId, status);
        if (!ok) return NotFound();
        return RedirectToAction(nameof(Details), new { id = orderId });
    }

    // Admin: local-store POS — staff creates a Local-channel order on behalf of a customer.
    [Authorize(Roles = "Admin,Staff")]
    [HttpPost("local-store/create")]
    public async Task<IActionResult> CreateLocalOrder([FromBody] CreateLocalOrderRequest request)
    {
        if (request == null || request.Quantity < 1)
        {
            return BadRequest("Invalid request.");
        }

        try
        {
            var order = await _orderService.CreateOrderForBookAsync(
                request.UserId,
                request.BookId,
                request.Quantity,
                channel: "Local",
                branchId: request.BranchId,
                staffId: request.StaffId
            );
            return Ok(new { orderId = order.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating local order: {ex.Message}");
            return BadRequest(ex.Message);
        }
    }

    // AJAX add-to-cart from book details (kept for API consumers)
    [HttpPost("{id:int}/add-item")]
    public async Task<IActionResult> AddToCart([FromRoute] int id, [FromBody] AddToCartRequest request)
    {
        try
        {
            var ok = await _orderService.AddOrderItemAsync(id, request.BookId, request.Quantity);
            if (!ok) return NotFound();
            await _orderService.CalculateTotalAsync(id);
            return Ok(new { message = "Item added to cart" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error adding to cart: {ex.Message}");
            return BadRequest(ex.Message);
        }
    }

    private int? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : (int?)null;
    }
}

public class AddToCartRequest
{
    public int BookId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class CreateLocalOrderRequest
{
    public int UserId { get; set; }
    public int BookId { get; set; }
    public int Quantity { get; set; } = 1;
    public int BranchId { get; set; }
    public int StaffId { get; set; }
}
