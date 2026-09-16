using BookShelf.Models;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Only configure if not already configured (e.g., from Program.cs)
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=bookshelf.db");
        }
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Book> Books { get; set; }
    public DbSet<Loan> Loans { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<AuthorProfile> AuthorProfiles { get; set; }
    public DbSet<AdminInvite> AdminInvites { get; set; }
    public DbSet<Branch> Branches { get; set; }
    public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
    public DbSet<UserSubscription> UserSubscriptions { get; set; }
    public DbSet<StaffProfile> StaffProfiles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure unique constraints
        modelBuilder.Entity<AuthorProfile>()
            .HasIndex(ap => ap.UserId)
            .IsUnique();

        modelBuilder.Entity<StaffProfile>()
            .HasIndex(sp => sp.UserId)
            .IsUnique();

        // ===== USER RELATIONSHIPS =====
        
        // User -> Orders (1 to many)
        modelBuilder.Entity<User>()
            .HasMany(u => u.Orders)
            .WithOne(o => o.User)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // User -> Loans (1 to many)
        modelBuilder.Entity<User>()
            .HasMany(u => u.Loans)
            .WithOne(l => l.User)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // User -> Subscriptions (1 to many)
        modelBuilder.Entity<User>()
            .HasMany(u => u.Subscriptions)
            .WithOne(us => us.User)
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // User -> AdminInvites (sent) (1 to many)
        modelBuilder.Entity<User>()
            .HasMany(u => u.AdminInvitesSent)
            .WithOne(ai => ai.CreatedByAdmin)
            .HasForeignKey(ai => ai.CreatedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        // User -> AuthorProfile (1 to 1)
        modelBuilder.Entity<User>()
            .HasOne(u => u.AuthorProfile)
            .WithOne(ap => ap.User)
            .HasForeignKey<AuthorProfile>(ap => ap.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // User -> StaffProfile (1 to 1)
        modelBuilder.Entity<User>()
            .HasOne(u => u.StaffProfile)
            .WithOne(sp => sp.User)
            .HasForeignKey<StaffProfile>(sp => sp.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // ===== ORDER RELATIONSHIPS =====
        
        // Order -> OrderItems
        modelBuilder.Entity<Order>()
            .HasMany(o => o.OrderItems)
            .WithOne(oi => oi.Order)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Order -> Payment (1 to 1, optional)
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Payment)
            .WithOne(p => p.Order)
            .HasForeignKey<Payment>(p => p.OrderId)
            .OnDelete(DeleteBehavior.SetNull);

        // Order -> Branch
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Branch)
            .WithMany(b => b.Orders)
            .HasForeignKey(o => o.BranchId)
            .OnDelete(DeleteBehavior.SetNull);

        // ===== BOOK RELATIONSHIPS =====
        
        // Book -> AuthorProfile
        modelBuilder.Entity<Book>()
            .HasOne(b => b.AuthorProfile)
            .WithMany(ap => ap.Books)
            .HasForeignKey(b => b.AuthorProfileId)
            .OnDelete(DeleteBehavior.SetNull);

        // Book -> OrderItems
        modelBuilder.Entity<Book>()
            .HasMany(b => b.OrderItems)
            .WithOne(oi => oi.Book)
            .HasForeignKey(oi => oi.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        // Book -> Loans
        modelBuilder.Entity<Book>()
            .HasMany(b => b.Loans)
            .WithOne(l => l.Book)
            .HasForeignKey(l => l.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        // ===== LOAN RELATIONSHIPS =====
        
        // Loan -> Branch
        modelBuilder.Entity<Loan>()
            .HasOne(l => l.Branch)
            .WithMany(b => b.Loans)
            .HasForeignKey(l => l.BranchId)
            .OnDelete(DeleteBehavior.SetNull);

        // ===== ORDERITEM RELATIONSHIPS =====
        
        // OrderItem is configured via Order and Book above

        // ===== AUTHORPROFILE RELATIONSHIPS =====
        
        // AuthorProfile -> ApprovedByAdmin (User who approved)
        modelBuilder.Entity<AuthorProfile>()
            .HasOne(ap => ap.ApprovedByAdmin)
            .WithMany()
            .HasForeignKey(ap => ap.ApprovedByAdminId)
            .OnDelete(DeleteBehavior.SetNull);

        // ===== STAFFPROFILE RELATIONSHIPS =====
        
        // StaffProfile -> Branch
        modelBuilder.Entity<StaffProfile>()
            .HasOne(sp => sp.Branch)
            .WithMany(b => b.Staff)
            .HasForeignKey(sp => sp.BranchId)
            .OnDelete(DeleteBehavior.Cascade);

        // ===== USERSUBSCRIPTION RELATIONSHIPS =====
        
        // UserSubscription -> SubscriptionPlan
        modelBuilder.Entity<UserSubscription>()
            .HasOne(us => us.Plan)
            .WithMany(sp => sp.UserSubscriptions)
            .HasForeignKey(us => us.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // UserSubscription -> Payment (1 to 1, optional)
        modelBuilder.Entity<UserSubscription>()
            .HasOne(us => us.Payment)
            .WithOne(p => p.Subscription)
            .HasForeignKey<Payment>(p => p.SubscriptionId)
            .OnDelete(DeleteBehavior.SetNull);

        // ===== PAYMENT RELATIONSHIPS =====
        
        // Payment -> Loan (for fine payments)
        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Loan)
            .WithMany()
            .HasForeignKey(p => p.LoanId)
            .OnDelete(DeleteBehavior.SetNull);

        // ===== SEED DATA =====
        
        // Seed roles
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Customer" },
            new Role { Id = 2, Name = "Admin" },
            new Role { Id = 3, Name = "Staff" }
        );
    }
}