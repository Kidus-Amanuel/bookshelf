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
            _logger.LogInformation("Starting database seeding/updating...");

            // 1. Fix existing books with broken via.placeholder.com images
            var existingBooks = await _context.Books.ToListAsync();
            bool coversUpdated = false;
            foreach (var b in existingBooks)
            {
                if (b.CoverImageUrl != null && b.CoverImageUrl.Contains("via.placeholder.com"))
                {
                    var slug = b.Title.ToLower().Replace(" ", "-").Replace("'", "");
                    b.CoverImageUrl = $"https://picsum.photos/seed/{slug}/300/450";
                    coversUpdated = true;
                }
            }
            if (coversUpdated)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("✓ Updated broken cover images on existing books.");
            }

            // Check if full seed is needed
            if (await _context.Users.AnyAsync())
            {
                // Full seed already done, but let's check if we need to add the EXTRA dummy books
                if (existingBooks.Count < 20)
                {
                    var firstAuthor = await _context.AuthorProfiles.FirstOrDefaultAsync(a => a.Status == "Approved");
                    if (firstAuthor != null)
                    {
                        var extraBooks = GetExtraBooks(firstAuthor.Id);
                        var titles = existingBooks.Select(b => b.Title).ToList();
                        var booksToAdd = extraBooks.Where(b => !titles.Contains(b.Title)).ToList();
                        if (booksToAdd.Any())
                        {
                            _context.Books.AddRange(booksToAdd);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation($"✓ Added {booksToAdd.Count} extra dummy books to existing database.");
                        }
                    }
                }

                _logger.LogInformation("Database already seeded. Skipping full seed.");
                return;
            }

            // ==========================================
            // FULL SEED FOR NEW DATABASE
            // ==========================================

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
                new() { Name = "Downtown Library", Address = "123 Main St, Springfield, IL 62701", Phone = "(555) 123-4567", CreatedAt = DateTime.UtcNow },
                new() { Name = "Westside Branch", Address = "456 Oak Ave, Springfield, IL 62702", Phone = "(555) 234-5678", CreatedAt = DateTime.UtcNow },
                new() { Name = "Eastside Hub", Address = "789 Elm Rd, Springfield, IL 62703", Phone = "(555) 345-6789", CreatedAt = DateTime.UtcNow }
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
                new() { FullName = "Alice Admin", Email = "admin@bookshelf.local", PasswordHash = _passwordHasher.HashPassword(new User(), "Admin123!"), RoleId = adminRole.Id, CreatedAt = DateTime.UtcNow.AddDays(-30) },
                new() { FullName = "Bob Customer", Email = "bob@example.local", PasswordHash = _passwordHasher.HashPassword(new User(), "Customer123!"), RoleId = customerRole.Id, CreatedAt = DateTime.UtcNow.AddDays(-20) },
                new() { FullName = "Carol Author", Email = "carol@example.local", PasswordHash = _passwordHasher.HashPassword(new User(), "Author123!"), RoleId = customerRole.Id, CreatedAt = DateTime.UtcNow.AddDays(-15) },
                new() { FullName = "Dave Staff", Email = "dave@bookshelf.local", PasswordHash = _passwordHasher.HashPassword(new User(), "Staff123!"), RoleId = staffRole.Id, CreatedAt = DateTime.UtcNow.AddDays(-10) },
                new() { FullName = "Emma Customer", Email = "emma@example.local", PasswordHash = _passwordHasher.HashPassword(new User(), "Customer123!"), RoleId = customerRole.Id, CreatedAt = DateTime.UtcNow.AddDays(-5) }
            };
            _context.Users.AddRange(users);
            await _context.SaveChangesAsync();
            _logger.LogInformation("✓ Seeded 5 users");

            // Seed Author Profiles
            var adminUser = users[0];
            var authorProfiles = new List<AuthorProfile>
            {
                new() { UserId = users[2].Id, PenName = "Carol Mystery", Bio = "Mystery and thriller writer with 10 years of experience.", Status = "Approved", AppliedAt = DateTime.UtcNow.AddDays(-14), ApprovedAt = DateTime.UtcNow.AddDays(-12), ApprovedByAdminId = adminUser.Id },
                new() { UserId = users[4].Id, PenName = "Emma Romance", Bio = "Romance and contemporary fiction author.", Status = "Approved", AppliedAt = DateTime.UtcNow.AddDays(-4), ApprovedAt = DateTime.UtcNow.AddDays(-2), ApprovedByAdminId = adminUser.Id }
            };
            _context.AuthorProfiles.AddRange(authorProfiles);
            await _context.SaveChangesAsync();
            _logger.LogInformation("✓ Seeded 2 approved author profiles");

            // Seed Staff Profiles
            _context.StaffProfiles.Add(new StaffProfile { UserId = users[3].Id, BranchId = branches[0].Id, StartDate = DateTime.UtcNow.AddDays(-10), IsActive = true });
            await _context.SaveChangesAsync();

            // Seed Subscription Plans
            var plans = new List<SubscriptionPlan>
            {
                new() { Name = "Free Trial", Price = 0m, DurationInDays = 30, MaxActiveLoans = 2, DiscountPercentOnPurchases = 0m, IsActive = true },
                new() { Name = "Reader Plus", Price = 9.99m, DurationInDays = 30, MaxActiveLoans = 5, DiscountPercentOnPurchases = 10m, IsActive = true },
                new() { Name = "Book Lover", Price = 19.99m, DurationInDays = 30, MaxActiveLoans = 10, DiscountPercentOnPurchases = 20m, IsActive = true },
                new() { Name = "Collector's Edition", Price = 49.99m, DurationInDays = 90, MaxActiveLoans = 20, DiscountPercentOnPurchases = 30m, IsActive = true }
            };
            _context.SubscriptionPlans.AddRange(plans);
            await _context.SaveChangesAsync();

            // Seed Books
            var books = new List<Book>
            {
                new() { Title = "The Silent Killer", Description = "A gripping mystery where a detective must solve a cold case before the killer strikes again.", ISBN = "978-1234567890", CoverImageUrl = "https://picsum.photos/seed/silent-killer/300/450", Price = 14.99m, IsForSale = true, IsForLoan = true, StockForSale = 25, StockForLoan = 10, Status = "Approved", AuthorProfileId = authorProfiles[0].Id, CreatedAt = DateTime.UtcNow.AddDays(-8) },
                new() { Title = "Secrets of Maple Lane", Description = "A cozy mystery set in a small town where everyone has secrets.", ISBN = "978-0987654321", CoverImageUrl = "https://picsum.photos/seed/maple-lane/300/450", Price = 12.99m, IsForSale = true, IsForLoan = true, StockForSale = 15, StockForLoan = 8, Status = "Approved", AuthorProfileId = authorProfiles[0].Id, CreatedAt = DateTime.UtcNow.AddDays(-5) },
                new() { Title = "Forever in Your Eyes", Description = "A heartwarming romance about second chances and unexpected love.", ISBN = "978-5555555555", CoverImageUrl = "https://picsum.photos/seed/forever-eyes/300/450", Price = 13.99m, IsForSale = true, IsForLoan = true, StockForSale = 20, StockForLoan = 12, Status = "Approved", AuthorProfileId = authorProfiles[1].Id, CreatedAt = DateTime.UtcNow.AddDays(-3) },
                new() { Title = "Midnight in Paris", Description = "A pending submission - awaiting admin review.", ISBN = "978-7777777777", CoverImageUrl = "https://picsum.photos/seed/midnight-paris/300/450", Price = 15.99m, IsForSale = true, IsForLoan = true, StockForSale = 10, StockForLoan = 5, Status = "Pending", AuthorProfileId = authorProfiles[1].Id, CreatedAt = DateTime.UtcNow.AddHours(-2) }
            };
            books.AddRange(GetExtraBooks(authorProfiles[0].Id));
            _context.Books.AddRange(books);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"✓ Seeded {books.Count} books");

            // Seed Orders
            var orders = new List<Order>
            {
                new() { UserId = users[1].Id, Channel = "Online", Status = "Pending", CreatedAt = DateTime.UtcNow.AddDays(-2) },
                new() { UserId = users[1].Id, Channel = "Local", BranchId = branches[0].Id, ProcessedByStaffId = users[3].Id, Status = "Completed", CreatedAt = DateTime.UtcNow.AddDays(-1) }
            };
            _context.Orders.AddRange(orders);
            await _context.SaveChangesAsync();

            // Seed Order Items
            _context.OrderItems.AddRange(new List<OrderItem>
            {
                new() { OrderId = orders[0].Id, BookId = books[0].Id, Quantity = 2, UnitPrice = 14.99m },
                new() { OrderId = orders[0].Id, BookId = books[1].Id, Quantity = 1, UnitPrice = 12.99m },
                new() { OrderId = orders[1].Id, BookId = books[2].Id, Quantity = 1, UnitPrice = 13.99m }
            });
            await _context.SaveChangesAsync();

            // Seed Loans
            var loans = new List<Loan>
            {
                new() { UserId = users[1].Id, BookId = books[0].Id, Channel = "Online", BorrowedAt = DateTime.UtcNow.AddDays(-10), DueAt = DateTime.UtcNow.AddDays(4), IsReturned = false, FineAmount = 0m, FinePaid = false },
                new() { UserId = users[1].Id, BookId = books[1].Id, Channel = "Local", BranchId = branches[0].Id, BorrowedAt = DateTime.UtcNow.AddDays(-5), DueAt = DateTime.UtcNow.AddDays(9), IsReturned = false, FineAmount = 0m, FinePaid = false },
                new() { UserId = users[4].Id, BookId = books[2].Id, Channel = "Online", BorrowedAt = DateTime.UtcNow.AddDays(-20), DueAt = DateTime.UtcNow.AddDays(-6), ReturnedAt = null, IsReturned = false, FineAmount = 7.00m, FinePaid = false },
                new() { UserId = users[4].Id, BookId = books[0].Id, Channel = "Local", BranchId = branches[0].Id, BorrowedAt = DateTime.UtcNow.AddDays(-15), DueAt = DateTime.UtcNow.AddDays(-1), ReturnedAt = DateTime.UtcNow.AddDays(-1), IsReturned = true, FineAmount = 0m, FinePaid = true }
            };
            _context.Loans.AddRange(loans);
            await _context.SaveChangesAsync();

            // Seed User Subscriptions
            var subscriptions = new List<UserSubscription>
            {
                new() { UserId = users[1].Id, PlanId = plans[1].Id, StartDate = DateTime.UtcNow.AddDays(-25), EndDate = DateTime.UtcNow.AddDays(5), Status = "Active", AutoRenew = true },
                new() { UserId = users[4].Id, PlanId = plans[2].Id, StartDate = DateTime.UtcNow.AddDays(-60), EndDate = DateTime.UtcNow.AddDays(-30), Status = "Expired", AutoRenew = false }
            };
            _context.UserSubscriptions.AddRange(subscriptions);
            await _context.SaveChangesAsync();

            // Seed Payments
            _context.Payments.AddRange(new List<Payment>
            {
                new() { OrderId = orders[0].Id, Amount = 42.97m, PaidAt = DateTime.UtcNow.AddDays(-2), Status = "Completed" },
                new() { SubscriptionId = subscriptions[0].Id, Amount = 9.99m, PaidAt = DateTime.UtcNow.AddDays(-25), Status = "Completed" },
                new() { LoanId = loans[2].Id, Amount = 7.00m, PaidAt = DateTime.UtcNow, Status = "Pending" }
            });
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ Database seeding completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding database");
            throw;
        }
    }

    private List<Book> GetExtraBooks(int authorProfileId)
    {
        return new List<Book>
        {
            // The original 6 extra books
            new() { Title = "The Lost City", Description = "An exciting adventure into an uncharted territory.", ISBN = "978-9999999901", CoverImageUrl = "https://picsum.photos/seed/lostcity/300/450", Price = 19.99m, IsForSale = true, IsForLoan = true, StockForSale = 10, StockForLoan = 5, Status = "Approved", AuthorProfileId = authorProfileId, CreatedAt = DateTime.UtcNow.AddDays(-10) },
            new() { Title = "Deep Ocean Mysteries", Description = "Discover the secrets of the deep blue.", ISBN = "978-9999999902", CoverImageUrl = "https://picsum.photos/seed/deepocean/300/450", Price = 11.99m, IsForSale = true, IsForLoan = false, StockForSale = 30, StockForLoan = 0, Status = "Approved", AuthorProfileId = authorProfileId, CreatedAt = DateTime.UtcNow.AddDays(-9) },
            new() { Title = "Culinary Delights", Description = "100 recipes from around the world.", ISBN = "978-9999999903", CoverImageUrl = "https://picsum.photos/seed/culinary/300/450", Price = 25.00m, IsForSale = true, IsForLoan = true, StockForSale = 15, StockForLoan = 5, Status = "Approved", AuthorProfileId = authorProfileId, CreatedAt = DateTime.UtcNow.AddDays(-7) },
            new() { Title = "History of the Future", Description = "A sci-fi epic spanning millennia.", ISBN = "978-9999999904", CoverImageUrl = "https://picsum.photos/seed/historyfuture/300/450", Price = 16.50m, IsForSale = true, IsForLoan = true, StockForSale = 20, StockForLoan = 10, Status = "Approved", AuthorProfileId = authorProfileId, CreatedAt = DateTime.UtcNow.AddDays(-6) },
            new() { Title = "Mindfulness Daily", Description = "Practical guide to meditation.", ISBN = "978-9999999905", CoverImageUrl = "https://picsum.photos/seed/mindfulness/300/450", Price = 9.99m, IsForSale = false, IsForLoan = true, StockForSale = 0, StockForLoan = 50, Status = "Approved", AuthorProfileId = authorProfileId, CreatedAt = DateTime.UtcNow.AddDays(-4) },
            new() { Title = "Financial Freedom", Description = "How to manage your money.", ISBN = "978-9999999906", CoverImageUrl = "https://picsum.photos/seed/finance/300/450", Price = 22.99m, IsForSale = true, IsForLoan = true, StockForSale = 100, StockForLoan = 20, Status = "Approved", AuthorProfileId = authorProfileId, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            
            // 10 new additional books
            new() { Title = "Shadows in the Mist", Description = "A terrifying thriller in a remote village.", ISBN = "978-9999999907", CoverImageUrl = "https://picsum.photos/seed/shadowsmist/300/450", Price = 14.50m, IsForSale = true, IsForLoan = true, StockForSale = 8, StockForLoan = 3, Status = "Approved", AuthorProfileId = authorProfileId, CreatedAt = DateTime.UtcNow.AddDays(-20) },
            new() { Title = "The Programmer's Journey", Description = "Navigating a career in tech.", ISBN = "978-9999999908", CoverImageUrl = "https://picsum.photos/seed/programmerjourney/300/450", Price = 35.00m, IsForSale = true, IsForLoan = true, StockForSale = 40, StockForLoan = 5, Status = "Approved", AuthorProfileId = authorProfileId, CreatedAt = DateTime.UtcNow.AddDays(-19) },
            new() { Title = "Stellar Horizons", Description = "Humanity's first steps beyond the solar system.", ISBN = "978-9999999909", CoverImageUrl = "https://picsum.photos/seed/stellarhorizons/300/450", Price = 18.99m, IsForSale = true, IsForLoan = true, StockForSale = 12, StockForLoan = 6, Status = "Approved", AuthorProfileId = authorProfileId, CreatedAt = DateTime.UtcNow.AddDays(-18) },
            new() { Title = "The Art of Baking", Description = "Mastering bread and pastries at home.", ISBN = "978-9999999910", CoverImageUrl = "https://picsum.photos/seed/artbaking/300/450", Price = 28.50m, IsForSale = true, IsForLoan = false, StockForSale = 20, StockForLoan = 0, Status = "Approved", AuthorProfileId = authorProfileId, CreatedAt = DateTime.UtcNow.AddDays(-17) },
            new() { Title = "Echoes of the Past", Description = "A historical drama set in the roaring twenties.", ISBN = "978-9999999911", CoverImageUrl = "https://picsum.photos/seed/echoespast/300/450", Price = 13.99m, IsForSale = true, IsForLoan = true, StockForSale = 5, StockForLoan = 2, Status = "Approved", AuthorProfileId = authorProfileId, CreatedAt = DateTime.UtcNow.AddDays(-16) },
            new() { Title = "Urban Jungle", Description = "Surviving the modern metropolis.", ISBN = "978-9999999912", CoverImageUrl = "https://picsum.photos/seed/urbanjungle/300/450", Price = 15.00m, IsForSale = true, IsForLoan = true, StockForSale = 25, StockForLoan = 10, Status = "Approved", AuthorProfileId = authorProfileId, CreatedAt = DateTime.UtcNow.AddDays(-15) },
            new() { Title = "Symphony of Souls", Description = "A poetic exploration of the human condition.", ISBN = "978-9999999913", CoverImageUrl = "https://picsum.photos/seed/symphonysouls/300/450", Price = 10.99m, IsForSale = false, IsForLoan = true, StockForSale = 0, StockForLoan = 15, Status = "Approved", AuthorProfileId = authorProfileId, CreatedAt = DateTime.UtcNow.AddDays(-14) },
            new() { Title = "Data Driven", Description = "How data is shaping our reality.", ISBN = "978-9999999914", CoverImageUrl = "https://picsum.photos/seed/datadriven/300/450", Price = 24.99m, IsForSale = true, IsForLoan = true, StockForSale = 35, StockForLoan = 12, Status = "Approved", AuthorProfileId = authorProfileId, CreatedAt = DateTime.UtcNow.AddDays(-13) },
            new() { Title = "The Last Stand", Description = "An apocalyptic survival story.", ISBN = "978-9999999915", CoverImageUrl = "https://picsum.photos/seed/laststand/300/450", Price = 16.99m, IsForSale = true, IsForLoan = true, StockForSale = 18, StockForLoan = 8, Status = "Approved", AuthorProfileId = authorProfileId, CreatedAt = DateTime.UtcNow.AddDays(-12) },
            new() { Title = "Garden Secrets", Description = "Growing your own food in small spaces.", ISBN = "978-9999999916", CoverImageUrl = "https://picsum.photos/seed/gardensecrets/300/450", Price = 19.50m, IsForSale = true, IsForLoan = true, StockForSale = 22, StockForLoan = 7, Status = "Approved", AuthorProfileId = authorProfileId, CreatedAt = DateTime.UtcNow.AddDays(-11) }
        };
    }
}
