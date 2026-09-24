using Microsoft.EntityFrameworkCore;

namespace EduMation.Data
{
    public interface IDesignTimeDbContextFactory<out TContext> where TContext : DbContext
    {
        TContext CreateDbContext(string[] args);
    }
}