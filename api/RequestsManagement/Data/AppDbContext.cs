using Microsoft.EntityFrameworkCore;
using RequestsManagement.Models;

namespace RequestsManagement.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<RequestEntity> Requests => Set<RequestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
