using BookShelf.Data;
using BookShelf.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Services;
/// <summary>
/// Seeds the database with sample data for development/testing
/// </summary>
public class DatabaseSeeder
{
    private readonly AppDbContext _context;
    private readonly ILogger<DatabaseSeeder> _logger;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public DatabaseSeeder(AppDbContext context, ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Seeds the database with sample data if it's empty
    /// </summary>
    public async Task SeedAsync()
    {
        try
        {
            // Check if data already exists
            if (await _context.Users.AnyAsync())
            {
                _logger.LogInformation("Database already seeded. Skipping seed.");
                return;
            }

            _logger.LogInformation("Starting database seeding...");

            // Seed Roles (should already exist from migration)
            var roles = await _context.Roles.ToListAsync();
            if (!roles.Any())
            {
                roles = new List<Role>
                {
                    new() { Name = "Customer" },
                    new() { Name = "Admin" },
                    new() { Name = "Staff" }
                };
                _context.Roles.AddRange(roles);
                await _context.SaveChangesAsync();
            }

            // Seed Branches
            var branches = new List<Branch>
            {
                new()
                {
                    Name = "Downtown Library",
                    Address = "123 Main St, Springfield, IL 62701",
                    Phone = "(555) 123-4567",
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "Westside Branch",
                    Address = "456 Oak Ave, Springfield, IL 62702",
                    Phone = "(555) 234-5678",
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "Eastside Hub",
                    Address = "789 Elm Rd, Springfield, IL 62703",
                    Phone = "(555) 345-6789",
                    CreatedAt = DateTime.UtcNow
                }
            };
            _context.Branches.AddRange(branches);
            await _context.SaveChangesAsync();
            _logger.LogInformation("✓ Seeded 3 branches");

            // Seed Users
            var customerRole = roles.First(r => r.Name == "Customer");
            var adminRole = roles.First(r => r.Name == "Admin");
            var staffRole = roles.First(r => r.Name == "Staff");

            var users = new List<User>
            {
                // Admin user
                new()
                {
                    FullName = "Alice Admin",
                    Email = "admin@bookshelf.local",
                    PasswordHash = _passwordHasher.HashPassword(new User(), "Admin123!"),
                    RoleId = adminRole.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-30)
                },
                // Customer user
                new()
                {
                    FullName = "Bob Customer",
                    Email = "bob@example.local",
                    PasswordHash = _passwordHasher.HashPassword(new User(), "Customer123!"),
                    RoleId = customerRole.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-20)
                },
                // Author user
                new()
                {
                    FullName = "Carol Author",
                    Email = "carol@example.local",
                    PasswordHash = _passwordHasher.HashPassword(new User(), "Author123!"),
                    RoleId = customerRole.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-15)
                },
                // Staff user
                new()
                {
                    FullName = "Dave Staff",
                    Email = "dave@bookshelf.local",
                    PasswordHash = _passwordHasher.HashPassword(new User(), "Staff123!"),
                    RoleId = staffRole.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                },
                // Another customer
                new()
                {
                    FullName = "Emma Customer",
                    Email = "emma@example.local",
                    PasswordHash = _passwordHasher.HashPassword(new User(), "Customer123!"),
                    RoleId = customerRole.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                }
            };
            _context.Users.AddRange(users);
            await _context.SaveChangesAsync();
            _logger.LogInformation("✓ Seeded 5 users");

            // Seed Author Profiles
            var adminUser = users[0];
            var authorUser = users[2];
            var anotherAuthorUser = users[4];

            var authorProfiles = new List<AuthorProfile>
            {
                new()
                {
                    UserId = authorUser.Id,
                    PenName = "Carol Mystery",
                    Bio = "Mystery and thriller writer with 10 years of experience.",
                    Status = "Approved",
                    AppliedAt = DateTime.UtcNow.AddDays(-14),
                    ApprovedAt = DateTime.UtcNow.AddDays(-12),
                    ApprovedByAdminId = adminUser.Id
                },
                new()
                {
                    UserId = anotherAuthorUser.Id,
                    PenName = "Emma Romance",
                    Bio = "Romance and contemporary fiction author.",
                    Status = "Approved",
                    AppliedAt = DateTime.UtcNow.AddDays(-4),
                    ApprovedAt = DateTime.UtcNow.AddDays(-2),
                    ApprovedByAdminId = adminUser.Id
                }
            };
            _context.AuthorProfiles.AddRange(authorProfiles);
            await _context.SaveChangesAsync();
            _logger.LogInformation("✓ Seeded 2 approved author profiles");

            // Seed Staff Profiles
            var staffUser = users[3];
            var downstownBranch = branches[0];

            var staffProfiles = new List<StaffProfile>
            {
                new()
                {
                    UserId = staffUser.Id,
                    BranchId = downstownBranch.Id,
                    StartDate = DateTime.UtcNow.AddDays(-10),
                    IsActive = true
                }
            };
            _context.StaffProfiles.AddRange(staffProfiles);
            await _context.SaveChangesAsync();
            _logger.LogInformation("✓ Seeded 1 staff profile");

            // Seed Subscription Plans
            var plans = new List<SubscriptionPlan>
            {
                new()
                {
                    Name = "Free Trial",
                    Price = 0m,
                    DurationInDays = 30,
                    MaxActiveLoans = 2,
                    DiscountPercentOnPurchases = 0m,
                    IsActive = true
                },
                new()
                {
                    Name = "Reader Plus",
                    Price = 9.99m,
                    DurationInDays = 30,
                    MaxActiveLoans = 5,
                    DiscountPercentOnPurchases = 10m,
                    IsActive = true
                },
                new()
                {
                    Name = "Book Lover",
                    Price = 19.99m,
                    DurationInDays = 30,
                    MaxActiveLoans = 10,
                    DiscountPercentOnPurchases = 20m,
                    IsActive = true
                },
                new()
                {
                    Name = "Collector's Edition",
                    Price = 49.99m,
                    DurationInDays = 90,
                    MaxActiveLoans = 20,
                    DiscountPercentOnPurchases = 30m,
                    IsActive = true
                }
            };
            _context.SubscriptionPlans.AddRange(plans);
            await _context.SaveChangesAsync();
            _logger.LogInformation("✓ Seeded 4 subscription plans");

            // Seed Books
            var books = new List<Book>
            {
                new()
                {
                    Title = "The Silent Killer",
                    Description = "A gripping mystery where a detective must solve a cold case before the killer strikes again.",
                    ISBN = "978-1234567890",
                    CoverImageUrl = "https://via.placeholder.com/300x450?text=Silent+Killer",
                    Price = 14.99m,
                    IsForSale = true,
                    IsForLoan = true,
                    StockForSale = 25,
                    StockForLoan = 10,
                    Status = "Approved",
                    AuthorProfileId = authorProfiles[0].Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-8)
                },
                new()
                {
                    Title = "Secrets of Maple Lane",
                    Description = "A cozy mystery set in a small town where everyone has secrets.",
                    ISBN = "978-0987654321",
                    CoverImageUrl = "https://via.placeholder.com/300x450?text=Maple+Lane",
                    Price = 12.99m,
                    IsForSale = true,
                    IsForLoan = true,
                    StockForSale = 15,
                    StockForLoan = 8,
                    Status = "Approved",
                    AuthorProfileId = authorProfiles[0].Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                },
                new()
                {
                    Title = "Forever in Your Eyes",
                    Description = "A heartwarming romance about second chances and unexpected love.",
                    ISBN = "978-5555555555",
                    CoverImageUrl = "https://via.placeholder.com/300x450?text=Forever+Eyes",
                    Price = 13.99m,
                    IsForSale = true,
                    IsForLoan = true,
                    StockForSale = 20,
                    StockForLoan = 12,
                    Status = "Approved",
                    AuthorProfileId = authorProfiles[1].Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                },
                new()
                {
                    Title = "Midnight in Paris",
                    Description = "A pending submission - awaiting admin review.",
                    ISBN = "978-7777777777",
                    CoverImageUrl = "https://via.placeholder.com/300x450?text=Midnight+Paris",
                    Price = 15.99m,
                    IsForSale = true,
                    IsForLoan = true,
                    StockForSale = 10,
                    StockForLoan = 5,
                    Status = "Pending",
                    AuthorProfileId = authorProfiles[1].Id,
                    CreatedAt = DateTime.UtcNow.AddHours(-2)
                }
            };
            _context.Books.AddRange(books);
            await _context.SaveChangesAsync();
            _logger.LogInformation("✓ Seeded 4 books (3 approved, 1 pending)");

            // Seed Orders
            var customerUser = users[1];
            var orders = new List<Order>
            {
                new()
                {
                    UserId = customerUser.Id,
                    Channel = "Online",
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                },
                new()
                {
                    UserId = customerUser.Id,
                    Channel = "Local",
                    BranchId = downstownBranch.Id,
                    ProcessedByStaffId = staffUser.Id,
                    Status = "Completed",
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                }
            };
            _context.Orders.AddRange(orders);
            await _context.SaveChangesAsync();
            _logger.LogInformation("✓ Seeded 2 orders");

            // Seed Order Items
            var orderItems = new List<OrderItem>
            {
                new()
                {
                    OrderId = orders[0].Id,
                    BookId = books[0].Id,
                    Quantity = 2,
                    UnitPrice = 14.99m
                },
                new()
                {
                    OrderId = orders[0].Id,
                    BookId = books[1].Id,
                    Quantity = 1,
                    UnitPrice = 12.99m
                },
                new()
                {
                    OrderId = orders[1].Id,
                    BookId = books[2].Id,
                    Quantity = 1,
                    UnitPrice = 13.99m
                }
            };
            _context.OrderItems.AddRange(orderItems);
            await _context.SaveChangesAsync();
            _logger.LogInformation("✓ Seeded 3 order items");

            // Seed Loans
            var loans = new List<Loan>
            {
                new()
                {
                    UserId = customerUser.Id,
                    BookId = books[0].Id,
                    Channel = "Online",
                    BorrowedAt = DateTime.UtcNow.AddDays(-10),
                    DueAt = DateTime.UtcNow.AddDays(4),
                    IsReturned = false,
                    FineAmount = 0m,
                    FinePaid = false
                },
                new()
                {
                    UserId = customerUser.Id,
                    BookId = books[1].Id,
                    Channel = "Local",
                    BranchId = downstownBranch.Id,
                    BorrowedAt = DateTime.UtcNow.AddDays(-5),
                    DueAt = DateTime.UtcNow.AddDays(9),
                    IsReturned = false,
                    FineAmount = 0m,
                    FinePaid = false
                },
                new()
                {
                    UserId = users[4].Id,
                    BookId = books[2].Id,
                    Channel = "Online",
                    BorrowedAt = DateTime.UtcNow.AddDays(-20),
                    DueAt = DateTime.UtcNow.AddDays(-6), // OVERDUE!
                    ReturnedAt = null,
                    IsReturned = false,
                    FineAmount = 7.00m, // 14 days * $0.50
                    FinePaid = false
                },
                new()
                {
                    UserId = users[4].Id,
                    BookId = books[0].Id,
                    Channel = "Local",
                    BranchId = downstownBranch.Id,
                    BorrowedAt = DateTime.UtcNow.AddDays(-15),
                    DueAt = DateTime.UtcNow.AddDays(-1),
                    ReturnedAt = DateTime.UtcNow.AddDays(-1),
                    IsReturned = true,
                    FineAmount = 0m,
                    FinePaid = true
                }
            };
            _context.Loans.AddRange(loans);
            await _context.SaveChangesAsync();
            _logger.LogInformation("✓ Seeded 4 loans (1 overdue, 1 returned)");

            // Seed User Subscriptions
            var subscriptions = new List<UserSubscription>
            {
                new()
                {
                    UserId = customerUser.Id,
                    PlanId = plans[1].Id, // Reader Plus
                    StartDate = DateTime.UtcNow.AddDays(-25),
                    EndDate = DateTime.UtcNow.AddDays(5),
                    Status = "Active",
                    AutoRenew = true
                },
                new()
                {
                    UserId = users[4].Id,
                    PlanId = plans[2].Id, // Book Lover
                    StartDate = DateTime.UtcNow.AddDays(-60),
                    EndDate = DateTime.UtcNow.AddDays(-30),
                    Status = "Expired",
                    AutoRenew = false
                }
            };
            _context.UserSubscriptions.AddRange(subscriptions);
            await _context.SaveChangesAsync();
            _logger.LogInformation("✓ Seeded 2 user subscriptions");

           // Seed Payments
var payments = new List<Payment>
{
    new()
    {
        OrderId = orders[0].Id,
        Amount = 42.97m, // (14.99 * 2) + 12.99
        PaidAt = DateTime.UtcNow.AddDays(-2),
        Status = "Completed"
    },
    new()
    {
        SubscriptionId = subscriptions[0].Id,
        Amount = 9.99m,
        PaidAt = DateTime.UtcNow.AddDays(-25),
        Status = "Completed"
    },
    new()
    {
        LoanId = loans[2].Id,
        Amount = 7.00m, // Fine payment
        PaidAt = DateTime.UtcNow,
        Status = "Pending"
    }
};
_context.Payments.AddRange(payments);
await _context.SaveChangesAsync();
_logger.LogInformation("✓ Seeded 3 payments");
          

            _logger.LogInformation("✅ Database seeding completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding database");
            throw;
        }
    }
}
