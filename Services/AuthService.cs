using BookShelf.Data;
using BookShelf.Models;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Services;

public interface IAuthService
{
    Task<AdminInvite> CreateAdminInviteAsync(string email, int createdByAdminId);
    Task<bool> IsValidInviteAsync(string email, string token);
    Task<User> CompleteAdminSignupAsync(string email, string token, string fullName, string passwordHash);
    Task<AuthorProfile> ApplyAsAuthorAsync(int userId, string penName, string bio);
    Task<AuthorProfile?> GetAuthorProfileAsync(int userId);
    Task<bool> ApproveAuthorApplicationAsync(int authorProfileId, int adminId);
    Task<bool> RejectAuthorApplicationAsync(int authorProfileId, int adminId);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext context, ILogger<AuthService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AdminInvite> CreateAdminInviteAsync(string email, int createdByAdminId)
    {
        // Check if user already exists
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (existingUser != null)
        {
            throw new InvalidOperationException("A user with this email already exists.");
        }

        var invite = new AdminInvite
        {
            Email = email,
            Token = Guid.NewGuid().ToString(),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedByAdminId = createdByAdminId
        };

        _context.AdminInvites.Add(invite);
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Admin invite created for {email}");

        return invite;
    }

    public async Task<bool> IsValidInviteAsync(string email, string token)
    {
        var invite = await _context.AdminInvites
            .FirstOrDefaultAsync(ai => ai.Email == email && ai.Token == token && !ai.IsUsed);

        if (invite == null)
            return false;

        return invite.ExpiresAt > DateTime.UtcNow;
    }

    public async Task<User> CompleteAdminSignupAsync(string email, string token, string fullName, string passwordHash)
    {
        var invite = await _context.AdminInvites
            .FirstOrDefaultAsync(ai => ai.Email == email && ai.Token == token && !ai.IsUsed);

        if (invite == null || invite.ExpiresAt < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Invalid or expired invite.");
        }

        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
        if (adminRole == null)
        {
            throw new InvalidOperationException("Admin role not found.");
        }

        var user = new User
        {
            FullName = fullName,
            Email = email,
            PasswordHash = passwordHash,
            RoleId = adminRole.Id
        };

        _context.Users.Add(user);
        invite.IsUsed = true;
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Admin user created for {email}");
        return user;
    }

    public async Task<AuthorProfile> ApplyAsAuthorAsync(int userId, string penName, string bio)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        var existingProfile = await _context.AuthorProfiles
            .FirstOrDefaultAsync(ap => ap.UserId == userId);

        if (existingProfile != null)
        {
            throw new InvalidOperationException("User already has an author profile.");
        }

        var profile = new AuthorProfile
        {
            UserId = userId,
            PenName = penName,
            Bio = bio,
            Status = "Pending"
        };

        _context.AuthorProfiles.Add(profile);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Author application submitted by user {userId}");
        return profile;
    }

    public async Task<AuthorProfile?> GetAuthorProfileAsync(int userId)
    {
        return await _context.AuthorProfiles
            .FirstOrDefaultAsync(ap => ap.UserId == userId);
    }

    public async Task<bool> ApproveAuthorApplicationAsync(int authorProfileId, int adminId)
    {
        var profile = await _context.AuthorProfiles.FindAsync(authorProfileId);
        if (profile == null)
        {
            return false;
        }

        profile.Status = "Approved";
        profile.ApprovedAt = DateTime.UtcNow;
        profile.ApprovedByAdminId = adminId;

        await _context.SaveChangesAsync();
        _logger.LogInformation($"Author profile {authorProfileId} approved by admin {adminId}");

        return true;
    }

    public async Task<bool> RejectAuthorApplicationAsync(int authorProfileId, int adminId)
    {
        var profile = await _context.AuthorProfiles.FindAsync(authorProfileId);
        if (profile == null)
        {
            return false;
        }

        profile.Status = "Rejected";
        profile.ApprovedByAdminId = adminId;

        await _context.SaveChangesAsync();
        _logger.LogInformation($"Author profile {authorProfileId} rejected by admin {adminId}");

        return true;
    }
}

