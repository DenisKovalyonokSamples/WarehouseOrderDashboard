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
    /// Verifies mixed order selection creates a picking task from pickable orders and skips non-pickable ones.
    /// </summary>
    [Fact]
    public async Task CreatePickingTask_MixedPickableAndNonPickable_SkipsNonPickableOrder()
    {
        await using var dbContext = CreateDbContext();
        await SeedReferenceDataAsync(dbContext);
        var orderWorkflowService = new OrderWorkflowService(dbContext);

        var nonPickableOrder = await orderWorkflowService.CreateOrderAsync(
            new CreateOrderRequest("ORD-2001", 1, 1, [new OrderLineCreateDto(1, 2)]),
            CancellationToken.None);

        var reservableOrder = await orderWorkflowService.CreateOrderAsync(
            new CreateOrderRequest("ORD-2002", 1, 1, [new OrderLineCreateDto(1, 4)]),
            CancellationToken.None);

        var reservedOrder = await orderWorkflowService.ChangeStatusAsync(
            reservableOrder.Id,
            new ChangeOrderStatusRequest(OrderStatus.Confirmed, reservableOrder.Version),
            CancellationToken.None);

        var pickingTask = await orderWorkflowService.CreatePickingTaskAsync(
            new CreatePickingTaskRequest([nonPickableOrder.Id, reservedOrder.Id]),
            CancellationToken.None);

        Assert.NotEmpty(pickingTask.Lines);

        var nonPickableStatus = await dbContext.WarehouseOrders.Where(x => x.Id == nonPickableOrder.Id).Select(x => x.Status).SingleAsync();
        var pickableStatus = await dbContext.WarehouseOrders.Where(x => x.Id == reservedOrder.Id).Select(x => x.Status).SingleAsync();

        Assert.Equal(OrderStatus.New, nonPickableStatus);
        Assert.Equal(OrderStatus.InPicking, pickableStatus);
    }

    /// <summary>
    /// Verifies creating a picking task with only non-pickable orders fails with a clear message.
    /// </summary>
    [Fact]
    public async Task CreatePickingTask_OnlyNonPickableOrders_ThrowsWithReason()
    {
        await using var dbContext = CreateDbContext();
        await SeedReferenceDataAsync(dbContext);
        var orderWorkflowService = new OrderWorkflowService(dbContext);

        var nonPickableOrder = await orderWorkflowService.CreateOrderAsync(
            new CreateOrderRequest("ORD-2003", 1, 1, [new OrderLineCreateDto(1, 2)]),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orderWorkflowService.CreatePickingTaskAsync(new CreatePickingTaskRequest([nonPickableOrder.Id]), CancellationToken.None));

        Assert.Contains("No reservable lines found for picking task.", exception.Message);
        Assert.Contains("Selected non-pickable orders", exception.Message);
    }

    /// <summary>
    /// Verifies orders query paging returns correct counts and page items.
    /// </summary>
    [Fact]
    public async Task GetOrdersAsync_ReturnsPagedResult()
    {
        await using var dbContext = CreateDbContext();
        await SeedReferenceDataAsync(dbContext);

        for (var i = 1; i <= 30; i++)
        {
            dbContext.WarehouseOrders.Add(new WarehouseOrder
            {
                OrderNumber = $"ORD-PAGE-{i:000}",
                CustomerId = 1,
                WarehouseId = 1,
                Status = OrderStatus.New,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                Lines = [new OrderLine { ItemId = 1, Quantity = 1 }]
            });
        }

        await dbContext.SaveChangesAsync();

        var orderWorkflowService = new OrderWorkflowService(dbContext);
        var page = await orderWorkflowService.GetOrdersAsync(new OrderQuery { Page = 2, PageSize = 25 }, CancellationToken.None);

        Assert.Equal(2, page.Page);
        Assert.Equal(25, page.PageSize);
        Assert.Equal(30, page.TotalCount);
        Assert.Equal(5, page.Items.Count);
    }

    /// <summary>
    /// Verifies stock overview paging and shortage projection.
    /// </summary>
    [Fact]
    public async Task GetStockOverviewAsync_ReturnsPagedResult_WithShortageFlag()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Customers.Add(new Customer { Id = 1, Code = "C-001", Name = "Contoso" });
        dbContext.Warehouses.Add(new WarehouseLocation { Id = 1, Code = "W-001", Name = "Main" });

        for (var i = 1; i <= 30; i++)
        {
            dbContext.Items.Add(new Item { Id = i, Sku = $"SKU-{i:000}", Name = $"Item {i}" });
            dbContext.StockBalances.Add(new StockBalance
            {
                Id = i,
                ItemId = i,
                WarehouseId = 1,
                AvailableQuantity = i == 1 ? 1 : 10,
                ReservedQuantity = i == 1 ? 2 : 1
            });
        }

        await dbContext.SaveChangesAsync();

        var orderWorkflowService = new OrderWorkflowService(dbContext);

        var page1 = await orderWorkflowService.GetStockOverviewAsync(null, 1, 25, CancellationToken.None);
        var page2 = await orderWorkflowService.GetStockOverviewAsync(null, 2, 25, CancellationToken.None);

        Assert.Equal(30, page1.TotalCount);
        Assert.Equal(25, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);
        Assert.Contains(page1.Items, x => x.ItemId == 1 && x.HasShortage);
    }

    /// <summary>
    /// Verifies picking task list paging and active-only filtering.
    /// </summary>
    [Fact]
    public async Task GetPickingTasksAsync_FiltersActive_AndPages()
    {
        await using var dbContext = CreateDbContext();
        await SeedReferenceDataAsync(dbContext);

        dbContext.PickingTasks.AddRange(
            new PickingTask { Id = 1, TaskNumber = "PT-001", Status = PickingTaskStatus.New, CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new PickingTask { Id = 2, TaskNumber = "PT-002", Status = PickingTaskStatus.InProgress, CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new PickingTask { Id = 3, TaskNumber = "PT-003", Status = PickingTaskStatus.Completed, CreatedAt = DateTime.UtcNow.AddMinutes(-3) },
            new PickingTask { Id = 4, TaskNumber = "PT-004", Status = PickingTaskStatus.Cancelled, CreatedAt = DateTime.UtcNow.AddMinutes(-4) });

        await dbContext.SaveChangesAsync();

        var orderWorkflowService = new OrderWorkflowService(dbContext);

        var activePage = await orderWorkflowService.GetPickingTasksAsync(true, 1, 25, CancellationToken.None);
        var allPage = await orderWorkflowService.GetPickingTasksAsync(false, 1, 2, CancellationToken.None);

        Assert.Equal(2, activePage.TotalCount);
        var excludedStatuses = new[] { "Completed", "Cancelled" };
        Assert.All(activePage.Items, x => Assert.DoesNotContain(x.Status, excludedStatuses));

        Assert.Equal(4, allPage.TotalCount);
        Assert.Equal(2, allPage.Items.Count);
        Assert.Equal(1, allPage.Page);
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
