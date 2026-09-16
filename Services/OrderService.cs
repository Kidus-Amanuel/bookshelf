using BookShelf.Data;
using BookShelf.Models;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Services;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(int userId, string channel, int? branchId, int? processedByStaffId);
    Task<Order> CreateOrderForBookAsync(int userId, int bookId, int quantity, string channel = "Online", int? branchId = null, int? staffId = null);
    Task<bool> AddOrderItemAsync(int orderId, int bookId, int quantity);
    Task<bool> UpdateOrderItemQuantityAsync(int orderId, int itemId, int quantity);
    Task<bool> RemoveOrderItemAsync(int orderId, int itemId);
    Task<bool> CalculateTotalAsync(int orderId);
    Task<Order?> GetOrderByIdAsync(int orderId);
    Task<Order?> GetOrCreateCartAsync(int userId, string channel = "Online");
    Task<IEnumerable<Order>> GetUserOrdersAsync(int userId);
    Task<IEnumerable<Order>> GetAllOrdersAsync(string? status = null, string? channel = null);
    Task<bool> UpdateOrderStatusAsync(int orderId, string status);
    Task<bool> ProcessCheckoutAsync(int orderId, decimal paidAmount, string paymentMethod);
    Task<Order?> PlaceOrderFromBookAsync(int userId, int bookId, int quantity, string paymentMethod);
}

public class OrderService : IOrderService
{
    private readonly AppDbContext _context;
    private readonly ILogger<OrderService> _logger;

    public OrderService(AppDbContext context, ILogger<OrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(int userId, string channel, int? branchId, int? processedByStaffId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        var order = new Order
        {
            UserId = userId,
            Channel = channel,
            BranchId = branchId,
            ProcessedByStaffId = processedByStaffId,
            Status = "Pending",
            TotalAmount = 0,
            Subtotal = 0,
            DiscountPercent = 0
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Order created for user {userId}");

        return order;
    }

    public async Task<Order> CreateOrderForBookAsync(int userId, int bookId, int quantity, string channel = "Online", int? branchId = null, int? staffId = null)
    {
        if (quantity < 1)
        {
            throw new InvalidOperationException("Quantity must be at least 1.");
        }

        var book = await _context.Books.FindAsync(bookId)
            ?? throw new InvalidOperationException("Book not found.");

        if (!book.IsForSale || book.StockForSale < quantity)
        {
            throw new InvalidOperationException("Book is not available for purchase in that quantity.");
        }

        var order = await CreateOrderAsync(userId, channel, branchId, staffId);
        await AddOrderItemAsync(order.Id, bookId, quantity);
        await CalculateTotalAsync(order.Id);

        return order;
    }

    public async Task<Order?> GetOrCreateCartAsync(int userId, string channel = "Online")
    {
        // Re-use the most-recent Pending cart for this user so refreshes don't create duplicates.
        var existing = await _context.Orders
            .Include(o => o.OrderItems)
            .Where(o => o.UserId == userId && o.Status == "Pending" && o.Channel == channel)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            return await GetOrderByIdAsync(existing.Id);
        }

        var created = await CreateOrderAsync(userId, channel, null, null);
        return await GetOrderByIdAsync(created.Id);
    }

    public async Task<bool> AddOrderItemAsync(int orderId, int bookId, int quantity)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null)
        {
            return false;
        }

        var book = await _context.Books.FindAsync(bookId);
        if (book == null)
        {
            return false;
        }

        if (!book.IsForSale)
        {
            throw new InvalidOperationException("Book is not available for purchase.");
        }

        var existingItem = await _context.OrderItems
            .FirstOrDefaultAsync(oi => oi.OrderId == orderId && oi.BookId == bookId);

        var desiredQty = (existingItem?.Quantity ?? 0) + quantity;
        if (book.StockForSale < desiredQty)
        {
            throw new InvalidOperationException($"Only {book.StockForSale} copies in stock; can't add {desiredQty}.");
        }

        if (existingItem != null)
        {
            existingItem.Quantity = desiredQty;
        }
        else
        {
            var orderItem = new OrderItem
            {
                OrderId = orderId,
                BookId = bookId,
                Quantity = quantity,
                UnitPrice = book.Price ?? 0
            };
            _context.OrderItems.Add(orderItem);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateOrderItemQuantityAsync(int orderId, int itemId, int quantity)
    {
        if (quantity < 1)
        {
            return await RemoveOrderItemAsync(orderId, itemId);
        }

        var item = await _context.OrderItems
            .Include(i => i.Book)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.OrderId == orderId);

        if (item == null)
        {
            return false;
        }

        if (item.Book.StockForSale < quantity)
        {
            throw new InvalidOperationException($"Only {item.Book.StockForSale} copies in stock; can't set quantity to {quantity}.");
        }

        item.Quantity = quantity;
        await _context.SaveChangesAsync();
        await CalculateTotalAsync(orderId);
        return true;
    }

    public async Task<bool> RemoveOrderItemAsync(int orderId, int itemId)
    {
        var item = await _context.OrderItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.OrderId == orderId);

        if (item == null)
        {
            return false;
        }

        _context.OrderItems.Remove(item);
        await _context.SaveChangesAsync();
        await CalculateTotalAsync(orderId);
        return true;
    }

    public async Task<bool> CalculateTotalAsync(int orderId)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            return false;
        }

        var subtotal = order.OrderItems.Sum(oi => oi.Quantity * oi.UnitPrice);

        // Apply subscription discount if applicable
        var subscription = await _context.UserSubscriptions
            .Where(us => us.UserId == order.UserId && us.Status == "Active")
            .Include(us => us.Plan)
            .FirstOrDefaultAsync();

        var discountPercent = subscription?.Plan.DiscountPercentOnPurchases ?? 0m;
        var total = subtotal * (1 - (discountPercent / 100m));

        order.Subtotal = subtotal;
        order.DiscountPercent = discountPercent;
        order.TotalAmount = total;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<Order?> GetOrderByIdAsync(int orderId)
    {
        return await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Book)
                    .ThenInclude(b => b.AuthorProfile)
            .Include(o => o.User)
            .Include(o => o.Branch)
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == orderId);
    }

    public async Task<IEnumerable<Order>> GetUserOrdersAsync(int userId)
    {
        return await _context.Orders
            .Where(o => o.UserId == userId)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Book)
            .Include(o => o.Branch)
            .Include(o => o.Payment)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Order>> GetAllOrdersAsync(string? status = null, string? channel = null)
    {
        IQueryable<Order> query = _context.Orders
            .Include(o => o.User)
            .Include(o => o.Branch)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Book)
            .Include(o => o.Payment);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(o => o.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(channel))
        {
            query = query.Where(o => o.Channel == channel);
        }

        return await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> UpdateOrderStatusAsync(int orderId, string status)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null)
        {
            return false;
        }

        order.Status = status;
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Order {orderId} status updated to {status}");

        return true;
    }

    public async Task<bool> ProcessCheckoutAsync(int orderId, decimal paidAmount, string paymentMethod)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            return false;
        }

        if (order.Status != "Pending")
        {
            throw new InvalidOperationException($"Order is already {order.Status}; cannot check out again.");
        }

        if (paidAmount < order.TotalAmount)
        {
            throw new InvalidOperationException("Payment amount is insufficient.");
        }

        // Reduce stock for each item
        foreach (var item in order.OrderItems)
        {
            var book = await _context.Books.FindAsync(item.BookId);
            if (book != null)
            {
                book.StockForSale = Math.Max(0, book.StockForSale - item.Quantity);
            }
        }

        order.Status = "Paid";
        var payment = new Payment
        {
            OrderId = orderId,
            Amount = paidAmount,
            Status = "Completed",
            PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "Card" : paymentMethod,
            PaidAt = DateTime.UtcNow
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Order {orderId} processed via {paymentMethod} for ${paidAmount}");

        return true;
    }

    public async Task<Order?> PlaceOrderFromBookAsync(int userId, int bookId, int quantity, string paymentMethod)
    {
        var order = await CreateOrderForBookAsync(userId, bookId, quantity);
        await ProcessCheckoutAsync(order.Id, order.TotalAmount, paymentMethod);
        return await GetOrderByIdAsync(order.Id);
    }
}
