using Microsoft.EntityFrameworkCore;
using Warehouse.Domain;

namespace Warehouse.Infrastructure;

/// <summary>
/// Entity Framework database context for warehouse domain entities.
/// </summary>
public sealed class AppDbContext : DbContext
{
    /// <summary>
    /// Initializes a new context instance.
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>
    /// Gets customers table.
    /// </summary>
    public DbSet<Customer> Customers => Set<Customer>();

    /// <summary>
    /// Gets warehouse locations table.
    /// </summary>
    public DbSet<WarehouseLocation> Warehouses => Set<WarehouseLocation>();

    /// <summary>
    /// Gets catalog items table.
    /// </summary>
    public DbSet<Item> Items => Set<Item>();

    /// <summary>
    /// Gets stock balances table.
    /// </summary>
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();

    /// <summary>
    /// Gets orders table.
    /// </summary>
    public DbSet<WarehouseOrder> WarehouseOrders => Set<WarehouseOrder>();

    /// <summary>
    /// Gets order lines table.
    /// </summary>
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    /// <summary>
    /// Gets stock reservations table.
    /// </summary>
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();

    /// <summary>
    /// Gets picking tasks table.
    /// </summary>
    public DbSet<PickingTask> PickingTasks => Set<PickingTask>();

    /// <summary>
    /// Gets picking task lines table.
    /// </summary>
    public DbSet<PickingTaskLine> PickingTaskLines => Set<PickingTaskLine>();

    /// <summary>
    /// Gets audit logs table.
    /// </summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>
    /// Saves changes and updates audit/concurrency fields on modified entities.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
                entry.Entity.Version++;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Configures model mapping, constraints, and indexes.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasMaxLength(250).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<WarehouseLocation>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasMaxLength(250).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Item>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Sku).HasMaxLength(80).IsRequired();
            e.Property(x => x.Name).HasMaxLength(250).IsRequired();
            e.HasIndex(x => x.Sku).IsUnique();
        });

        modelBuilder.Entity<StockBalance>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.AvailableQuantity).HasPrecision(18, 3);
            e.Property(x => x.ReservedQuantity).HasPrecision(18, 3);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.ItemId, x.WarehouseId }).IsUnique();
            e.HasOne(x => x.Item).WithMany(x => x.StockBalances).HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Warehouse).WithMany(x => x.StockBalances).HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WarehouseOrder>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OrderNumber).HasMaxLength(80).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.Property(x => x.CreatedAt).IsRequired();
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.HasIndex(x => new { x.WarehouseId, x.Status, x.CreatedAt });
            e.HasIndex(x => new { x.CustomerId, x.CreatedAt });
            e.HasOne(x => x.Customer).WithMany(x => x.Orders).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Warehouse).WithMany(x => x.Orders).HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderLine>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Property(x => x.ReservedQuantity).HasPrecision(18, 3);
            e.Property(x => x.PickedQuantity).HasPrecision(18, 3);
            e.HasIndex(x => x.OrderId);
            e.HasIndex(x => x.ItemId);
            e.HasOne(x => x.Order).WithMany(x => x.Lines).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Item).WithMany(x => x.OrderLines).HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockReservation>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.HasIndex(x => x.OrderLineId);
            e.HasIndex(x => new { x.ItemId, x.WarehouseId });
            e.HasOne(x => x.OrderLine).WithMany(x => x.Reservations).HasForeignKey(x => x.OrderLineId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PickingTask>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TaskNumber).HasMaxLength(80).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.TaskNumber).IsUnique();
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });

        modelBuilder.Entity<PickingTaskLine>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Property(x => x.PickedQuantity).HasPrecision(18, 3);
            e.HasIndex(x => x.PickingTaskId);
            e.HasIndex(x => x.OrderLineId);
            e.HasOne(x => x.PickingTask).WithMany(x => x.Lines).HasForeignKey(x => x.PickingTaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.OrderLine).WithMany(x => x.PickingTaskLines).HasForeignKey(x => x.OrderLineId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EntityName).HasMaxLength(120).IsRequired();
            e.Property(x => x.Action).HasMaxLength(80).IsRequired();
            e.Property(x => x.Details).HasMaxLength(2000);
            e.HasIndex(x => new { x.EntityName, x.EntityId, x.CreatedAt });
        });
    }
}
