using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Contracts;
using Warehouse.Domain;
using Warehouse.Infrastructure;
using Warehouse.Infrastructure.Services;
using Xunit;

namespace Warehouse.Tests;

public class WarehouseOrderTests
{
    [Fact]
    public async Task ConfirmOrder_ReservesStock_AndSetsReservedStatus()
    {
        await using var db = CreateDbContext();
        await SeedReferenceDataAsync(db);

        var service = new OrderWorkflowService(db);
        var created = await service.CreateOrderAsync(
            new CreateOrderRequest(
                "ORD-1001",
                1,
                1,
                [new OrderLineCreateDto(1, 5)]),
            CancellationToken.None);

        var updated = await service.ChangeStatusAsync(created.Id, new ChangeOrderStatusRequest(OrderStatus.Confirmed, created.Version), CancellationToken.None);

        Assert.Equal(OrderStatus.Reserved, updated.Status);
        Assert.Single(updated.Lines);
        Assert.Equal(5, updated.Lines.First().ReservedQuantity);
    }

    [Fact]
    public async Task CancelShippedOrder_Throws()
    {
        await using var db = CreateDbContext();
        await SeedReferenceDataAsync(db);
        var service = new OrderWorkflowService(db);

        var created = await service.CreateOrderAsync(
            new CreateOrderRequest("ORD-1002", 1, 1, [new OrderLineCreateDto(1, 1)]),
            CancellationToken.None);

        var reserved = await service.ChangeStatusAsync(created.Id, new ChangeOrderStatusRequest(OrderStatus.Confirmed, created.Version), CancellationToken.None);
        var inPicking = await service.ChangeStatusAsync(created.Id, new ChangeOrderStatusRequest(OrderStatus.InPicking, reserved.Version), CancellationToken.None);
        var picked = await service.ChangeStatusAsync(created.Id, new ChangeOrderStatusRequest(OrderStatus.Picked, inPicking.Version), CancellationToken.None);
        var shipped = await service.ChangeStatusAsync(created.Id, new ChangeOrderStatusRequest(OrderStatus.Shipped, picked.Version), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelOrderAsync(shipped.Id, shipped.Version, CancellationToken.None));
    }

    [Fact]
    public async Task CreatePickingTask_AndCompleteLine_UpdatesTaskProgress()
    {
        await using var db = CreateDbContext();
        await SeedReferenceDataAsync(db);
        var service = new OrderWorkflowService(db);

        var order = await service.CreateOrderAsync(
            new CreateOrderRequest("ORD-1003", 1, 1, [new OrderLineCreateDto(1, 3)]),
            CancellationToken.None);

        var reserved = await service.ChangeStatusAsync(order.Id, new ChangeOrderStatusRequest(OrderStatus.Confirmed, order.Version), CancellationToken.None);
        var pickingTask = await service.CreatePickingTaskAsync(new CreatePickingTaskRequest([reserved.Id]), CancellationToken.None);
        var line = pickingTask.Lines.Single();

        var completedTask = await service.CompletePickingLineAsync(line.Id, new CompletePickingLineRequest(3, 0), CancellationToken.None);

        Assert.Equal("Completed", completedTask.Status);
        Assert.Equal(3, completedTask.Lines.Single().PickedQuantity);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static async Task SeedReferenceDataAsync(AppDbContext db)
    {
        db.Customers.Add(new Customer { Id = 1, Code = "C-001", Name = "Contoso" });
        db.Warehouses.Add(new WarehouseLocation { Id = 1, Code = "W-001", Name = "Main" });
        db.Items.Add(new Item { Id = 1, Sku = "SKU-001", Name = "Widget" });
        db.StockBalances.Add(new StockBalance { Id = 1, ItemId = 1, WarehouseId = 1, AvailableQuantity = 100, ReservedQuantity = 0 });

        await db.SaveChangesAsync();
    }
}
