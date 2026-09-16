using BookShelf.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BookShelf;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        
        // Use SQLite for migration design-time
        optionsBuilder.UseSqlite("Data Source=bookshelf.db");

        return new AppDbContext(optionsBuilder.Options);
    }
}
