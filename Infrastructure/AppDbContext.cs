using Microsoft.EntityFrameworkCore;
using Warehouse.Domain;

namespace Warehouse.Infrastructure;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<WarehouseOrder> WarehouseOrders => Set<WarehouseOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WarehouseOrder>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.CreatedAt).IsRequired();
            e.HasIndex(x => x.CreatedAt);
        });
    }
}
