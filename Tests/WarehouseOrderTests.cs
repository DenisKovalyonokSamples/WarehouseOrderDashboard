using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Contracts;
using Warehouse.Domain;
using Warehouse.Infrastructure;
using Warehouse.Infrastructure.Services;
using Xunit;

namespace Warehouse.Tests;

/// <summary>
/// Integration-style workflow tests for order, stock reservation, and picking flows.
/// </summary>
public class WarehouseOrderTests
{
    /// <summary>
    /// Verifies confirming an order reserves stock and sets reserved status.
    /// </summary>
    [Fact]
    public async Task ConfirmOrder_ReservesStock_AndSetsReservedStatus()
    {
        await using var dbContext = CreateDbContext();
        await SeedReferenceDataAsync(dbContext);

        var orderWorkflowService = new OrderWorkflowService(dbContext);
        var createdOrder = await orderWorkflowService.CreateOrderAsync(
            new CreateOrderRequest(
                "ORD-1001",
                1,
                1,
                [new OrderLineCreateDto(1, 5)]),
            CancellationToken.None);

        var updatedOrder = await orderWorkflowService.ChangeStatusAsync(createdOrder.Id, new ChangeOrderStatusRequest(OrderStatus.Confirmed, createdOrder.Version), CancellationToken.None);

        Assert.Equal(OrderStatus.Reserved, updatedOrder.Status);
        Assert.Single(updatedOrder.Lines);
        Assert.Equal(5, updatedOrder.Lines.First().ReservedQuantity);
    }

    /// <summary>
    /// Verifies shipped orders cannot be cancelled.
    /// </summary>
    [Fact]
    public async Task CancelShippedOrder_Throws()
    {
        await using var dbContext = CreateDbContext();
        await SeedReferenceDataAsync(dbContext);
        var orderWorkflowService = new OrderWorkflowService(dbContext);

        var createdOrder = await orderWorkflowService.CreateOrderAsync(
            new CreateOrderRequest("ORD-1002", 1, 1, [new OrderLineCreateDto(1, 1)]),
            CancellationToken.None);

        var reservedOrder = await orderWorkflowService.ChangeStatusAsync(createdOrder.Id, new ChangeOrderStatusRequest(OrderStatus.Confirmed, createdOrder.Version), CancellationToken.None);
        var inPickingOrder = await orderWorkflowService.ChangeStatusAsync(createdOrder.Id, new ChangeOrderStatusRequest(OrderStatus.InPicking, reservedOrder.Version), CancellationToken.None);
        var pickedOrder = await orderWorkflowService.ChangeStatusAsync(createdOrder.Id, new ChangeOrderStatusRequest(OrderStatus.Picked, inPickingOrder.Version), CancellationToken.None);
        var shippedOrder = await orderWorkflowService.ChangeStatusAsync(createdOrder.Id, new ChangeOrderStatusRequest(OrderStatus.Shipped, pickedOrder.Version), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orderWorkflowService.CancelOrderAsync(shippedOrder.Id, shippedOrder.Version, CancellationToken.None));
    }

    /// <summary>
    /// Verifies picking completion updates task status and picked quantity.
    /// </summary>
    [Fact]
    public async Task CreatePickingTask_AndCompleteLine_UpdatesTaskProgress()
    {
        await using var dbContext = CreateDbContext();
        await SeedReferenceDataAsync(dbContext);
        var orderWorkflowService = new OrderWorkflowService(dbContext);

        var createdOrder = await orderWorkflowService.CreateOrderAsync(
            new CreateOrderRequest("ORD-1003", 1, 1, [new OrderLineCreateDto(1, 3)]),
            CancellationToken.None);

        var reservedOrder = await orderWorkflowService.ChangeStatusAsync(createdOrder.Id, new ChangeOrderStatusRequest(OrderStatus.Confirmed, createdOrder.Version), CancellationToken.None);
        var createdPickingTask = await orderWorkflowService.CreatePickingTaskAsync(new CreatePickingTaskRequest([reservedOrder.Id]), CancellationToken.None);
        var pickingTaskLine = createdPickingTask.Lines.Single();

        var completedPickingTask = await orderWorkflowService.CompletePickingLineAsync(pickingTaskLine.Id, new CompletePickingLineRequest(3, 0), CancellationToken.None);

        Assert.Equal("Completed", completedPickingTask.Status);
        Assert.Equal(3, completedPickingTask.Lines.Single().PickedQuantity);
    }

    /// <summary>
    /// Creates an isolated in-memory database context for tests.
    /// </summary>
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>
    /// Seeds required reference entities for workflow tests.
    /// </summary>
    private static async Task SeedReferenceDataAsync(AppDbContext dbContext)
    {
        dbContext.Customers.Add(new Customer { Id = 1, Code = "C-001", Name = "Contoso" });
        dbContext.Warehouses.Add(new WarehouseLocation { Id = 1, Code = "W-001", Name = "Main" });
        dbContext.Items.Add(new Item { Id = 1, Sku = "SKU-001", Name = "Widget" });
        dbContext.StockBalances.Add(new StockBalance { Id = 1, ItemId = 1, WarehouseId = 1, AvailableQuantity = 100, ReservedQuantity = 0 });

        await dbContext.SaveChangesAsync();
    }
}
