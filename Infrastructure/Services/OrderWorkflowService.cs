using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Linq.Expressions;
using Warehouse.Application.Contracts;
using Warehouse.Application.Services;
using Warehouse.Domain;

namespace Warehouse.Infrastructure.Services;

/// <summary>
/// Implements warehouse order workflow operations over <see cref="AppDbContext"/>.
/// </summary>
public sealed class OrderWorkflowService(AppDbContext dbContext) : IOrderWorkflowService
{
    /// <inheritdoc />
    public async Task<OrderDetailsDto> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0)
        {
            throw new InvalidOperationException("Order must contain at least one line.");
        }

        var order = new WarehouseOrder
        {
            OrderNumber = request.OrderNumber,
            CustomerId = request.CustomerId,
            WarehouseId = request.WarehouseId,
            Status = OrderStatus.New,
            Lines = request.Lines.Select(l => new OrderLine
            {
                ItemId = l.ItemId,
                Quantity = l.Quantity
            }).ToList()
        };

        dbContext.WarehouseOrders.Add(order);
        dbContext.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(WarehouseOrder),
            Action = "Created",
            Details = $"Order {order.OrderNumber} created."
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetOrderInternalAsync(order.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResult<OrderListItemDto>> GetOrdersAsync(OrderQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var ordersQuery = dbContext.WarehouseOrders
            .AsNoTracking()
            .Include(order => order.Customer)
            .Include(order => order.Warehouse)
            .Include(order => order.Lines)
            .AsQueryable();

        if (query.CreatedFromUtc.HasValue)
        {
            ordersQuery = ordersQuery.Where(order => order.CreatedAt >= query.CreatedFromUtc.Value);
        }

        if (query.CreatedToUtc.HasValue)
        {
            ordersQuery = ordersQuery.Where(order => order.CreatedAt <= query.CreatedToUtc.Value);
        }

        if (query.Status.HasValue)
        {
            ordersQuery = ordersQuery.Where(order => order.Status == query.Status.Value);
        }

        if (query.CustomerId.HasValue)
        {
            ordersQuery = ordersQuery.Where(order => order.CustomerId == query.CustomerId.Value);
        }

        if (query.WarehouseId.HasValue)
        {
            ordersQuery = ordersQuery.Where(order => order.WarehouseId == query.WarehouseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            ordersQuery = ordersQuery.Where(order => order.OrderNumber.Contains(search) || order.Customer.Name.Contains(search));
        }

        var totalCount = await ordersQuery.CountAsync(cancellationToken);

        var items = await ordersQuery
            .OrderByDescending(order => order.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(order => new OrderListItemDto(
                order.Id,
                order.OrderNumber,
                order.CustomerId,
                order.Customer.Name,
                order.WarehouseId,
                order.Warehouse.Name,
                order.Status,
                order.CreatedAt,
                order.Lines.Count))
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderListItemDto>(items, page, pageSize, totalCount);
    }

    /// <inheritdoc />
    public async Task<OrderDetailsDto?> GetOrderByIdAsync(int orderId, CancellationToken cancellationToken)
    {
        return await dbContext.WarehouseOrders
            .AsNoTracking()
            .Where(order => order.Id == orderId)
            .Select(ToOrderDetails())
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrderDetailsDto> ChangeStatusAsync(int orderId, ChangeOrderStatusRequest request, CancellationToken cancellationToken)
    {
        var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);

        var order = await dbContext.WarehouseOrders
            .Include(orderToUpdate => orderToUpdate.Lines)
            .FirstOrDefaultAsync(orderToUpdate => orderToUpdate.Id == orderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order not found.");

        EnsureVersion(order, request.ExpectedVersion);
        EnsureTransition(order.Status, request.TargetStatus);

        if (request.TargetStatus == OrderStatus.Confirmed)
        {
            await ReserveAsync(order, cancellationToken);
        }

        if (request.TargetStatus == OrderStatus.InPicking && order.PickingStartedAt is null)
        {
            order.PickingStartedAt = DateTime.UtcNow;
        }

        if (request.TargetStatus == OrderStatus.Shipped)
        {
            order.ShippedAt = DateTime.UtcNow;
        }

        if (request.TargetStatus != OrderStatus.Confirmed)
        {
            order.Status = request.TargetStatus;
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(WarehouseOrder),
            EntityId = order.Id,
            Action = "StatusChanged",
            Details = $"Status changed to {order.Status}."
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            await transaction.DisposeAsync();
        }

        return await GetOrderInternalAsync(orderId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CancelOrderAsync(int orderId, long expectedVersion, CancellationToken cancellationToken)
    {
        var result = await ChangeStatusAsync(orderId, new ChangeOrderStatusRequest(OrderStatus.Cancelled, expectedVersion), cancellationToken);
        _ = result;
    }

    /// <inheritdoc />
    public async Task<PickingTaskDto> CreatePickingTaskAsync(CreatePickingTaskRequest request, CancellationToken cancellationToken)
    {
        if (request.OrderIds.Count == 0)
        {
            throw new InvalidOperationException("At least one order is required.");
        }

        var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);

        var ordersToPick = await dbContext.WarehouseOrders
            .Include(order => order.Lines)
            .Where(order => request.OrderIds.Contains(order.Id))
            .ToListAsync(cancellationToken);

        if (ordersToPick.Count != request.OrderIds.Count)
        {
            throw new KeyNotFoundException("One or more orders not found.");
        }

        var pickingTask = new PickingTask
        {
            TaskNumber = $"PT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
            Status = PickingTaskStatus.New
        };

        var skippedOrders = new List<string>();

        foreach (var order in ordersToPick)
        {
            if (order.Status is not (OrderStatus.Reserved or OrderStatus.PartiallyReserved or OrderStatus.Confirmed))
            {
                skippedOrders.Add(order.OrderNumber);
                continue;
            }

            foreach (var orderLine in order.Lines.Where(orderLine => orderLine.ReservedQuantity - orderLine.PickedQuantity > 0))
            {
                pickingTask.Lines.Add(new PickingTaskLine
                {
                    OrderLineId = orderLine.Id,
                    Quantity = orderLine.ReservedQuantity - orderLine.PickedQuantity
                });
            }

            order.Status = OrderStatus.InPicking;
            order.PickingStartedAt ??= DateTime.UtcNow;
        }

        if (pickingTask.Lines.Count == 0)
        {
            var reason = skippedOrders.Count > 0
                ? $" Selected non-pickable orders: {string.Join(", ", skippedOrders)}."
                : string.Empty;

            throw new InvalidOperationException($"No reservable lines found for picking task.{reason}");
        }

        dbContext.PickingTasks.Add(pickingTask);
        dbContext.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(PickingTask),
            Action = "Created",
            Details = $"Task {pickingTask.TaskNumber} created for {ordersToPick.Count} orders."
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            await transaction.DisposeAsync();
        }

        return await GetPickingTaskInternalAsync(pickingTask.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PickingTaskDto> CompletePickingLineAsync(int pickingTaskLineId, CompletePickingLineRequest request, CancellationToken cancellationToken)
    {
        var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);

        var pickingTaskLine = await dbContext.PickingTaskLines
            .Include(line => line.PickingTask)
            .ThenInclude(pickingTask => pickingTask.Lines)
            .Include(line => line.OrderLine)
            .ThenInclude(orderLine => orderLine.Order)
            .ThenInclude(order => order.Lines)
            .FirstOrDefaultAsync(line => line.Id == pickingTaskLineId, cancellationToken)
            ?? throw new KeyNotFoundException("Picking line not found.");

        EnsureVersion(pickingTaskLine, request.ExpectedVersion);

        var remainingQuantity = pickingTaskLine.Quantity - pickingTaskLine.PickedQuantity;
        if (request.Quantity <= 0 || request.Quantity > remainingQuantity)
        {
            throw new InvalidOperationException("Invalid picked quantity.");
        }

        var stockBalance = await dbContext.StockBalances
            .FirstOrDefaultAsync(stock => stock.ItemId == pickingTaskLine.OrderLine.ItemId && stock.WarehouseId == pickingTaskLine.OrderLine.Order.WarehouseId, cancellationToken)
            ?? throw new KeyNotFoundException("Stock record not found.");

        pickingTaskLine.PickedQuantity += request.Quantity;
        pickingTaskLine.OrderLine.PickedQuantity += request.Quantity;

        stockBalance.ReservedQuantity = Math.Max(0, stockBalance.ReservedQuantity - request.Quantity);
        stockBalance.AvailableQuantity = Math.Max(0, stockBalance.AvailableQuantity - request.Quantity);

        pickingTaskLine.PickingTask.Status = pickingTaskLine.PickingTask.Lines.All(taskLine => taskLine.PickedQuantity >= taskLine.Quantity)
            ? PickingTaskStatus.Completed
            : PickingTaskStatus.InProgress;

        if (pickingTaskLine.OrderLine.Order.Lines.All(orderLine => orderLine.PickedQuantity >= orderLine.Quantity))
        {
            pickingTaskLine.OrderLine.Order.Status = OrderStatus.Picked;
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(PickingTaskLine),
            EntityId = pickingTaskLine.Id,
            Action = "Picked",
            Details = $"Picked quantity {request.Quantity}."
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            await transaction.DisposeAsync();
        }

        return await GetPickingTaskInternalAsync(pickingTaskLine.PickingTaskId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<StockOverviewDto>> GetStockOverviewAsync(int? warehouseId, CancellationToken cancellationToken)
    {
        var stockOverviewQuery = dbContext.StockBalances
            .AsNoTracking()
            .Include(stock => stock.Item)
            .Include(stock => stock.Warehouse)
            .AsQueryable();

        if (warehouseId.HasValue)
        {
            stockOverviewQuery = stockOverviewQuery.Where(stock => stock.WarehouseId == warehouseId);
        }

        var rows = await stockOverviewQuery
            .OrderBy(stock => stock.Item.Sku)
            .Select(stock => new
            {
                stock.ItemId,
                ItemSku = stock.Item.Sku,
                ItemName = stock.Item.Name,
                stock.WarehouseId,
                WarehouseName = stock.Warehouse.Name,
                stock.AvailableQuantity,
                stock.ReservedQuantity
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(stock => new StockOverviewDto(
                stock.ItemId,
                stock.ItemSku,
                stock.ItemName,
                stock.WarehouseId,
                stock.WarehouseName,
                stock.AvailableQuantity,
                stock.ReservedQuantity,
                stock.AvailableQuantity - stock.ReservedQuantity < 0))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<DashboardDto> GetDashboardAsync(DateOnly day, CancellationToken cancellationToken)
    {
        var dayStartUtc = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEndUtc = day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var todayOrderCount = await dbContext.WarehouseOrders.AsNoTracking().CountAsync(order => order.CreatedAt >= dayStartUtc && order.CreatedAt < dayEndUtc, cancellationToken);
        var overdueTasks = await dbContext.PickingTasks.AsNoTracking().CountAsync(pickingTask => pickingTask.Status != PickingTaskStatus.Completed && pickingTask.CreatedAt < DateTime.UtcNow.AddHours(-4), cancellationToken);
        var unfulfilledOrders = await dbContext.WarehouseOrders.AsNoTracking().CountAsync(order => order.Status != OrderStatus.Shipped && order.Status != OrderStatus.Cancelled, cancellationToken);

        var ordersWithPickingStart = await dbContext.WarehouseOrders
            .AsNoTracking()
            .Where(order => order.PickingStartedAt.HasValue)
            .Select(order => new { order.CreatedAt, order.PickingStartedAt })
            .ToListAsync(cancellationToken);

        var averageMinutesToPickingStart = ordersWithPickingStart.Count == 0
            ? 0
            : ordersWithPickingStart.Average(order => (order.PickingStartedAt!.Value - order.CreatedAt).TotalMinutes);

        return new DashboardDto(todayOrderCount, overdueTasks, unfulfilledOrders, averageMinutesToPickingStart);
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfRelationalAsync(CancellationToken cancellationToken)
    {
        return dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
    }

    private async Task ReserveAsync(WarehouseOrder order, CancellationToken cancellationToken)
    {
        foreach (var orderLine in order.Lines)
        {
            var quantityToReserve = orderLine.Quantity - orderLine.ReservedQuantity;
            if (quantityToReserve <= 0)
            {
                continue;
            }

            var stockBalance = await dbContext.StockBalances
                .FirstOrDefaultAsync(stock => stock.ItemId == orderLine.ItemId && stock.WarehouseId == order.WarehouseId, cancellationToken)
                ?? throw new InvalidOperationException($"Stock not found for item {orderLine.ItemId} and warehouse {order.WarehouseId}.");

            var availableUnreservedQuantity = Math.Max(0, stockBalance.AvailableQuantity - stockBalance.ReservedQuantity);
            var reservableQuantity = Math.Min(availableUnreservedQuantity, quantityToReserve);

            if (reservableQuantity <= 0)
            {
                continue;
            }

            stockBalance.ReservedQuantity += reservableQuantity;
            orderLine.ReservedQuantity += reservableQuantity;

            dbContext.StockReservations.Add(new StockReservation
            {
                OrderLineId = orderLine.Id,
                ItemId = orderLine.ItemId,
                WarehouseId = order.WarehouseId,
                Quantity = reservableQuantity
            });
        }

        order.Status = order.Lines.All(orderLine => orderLine.ReservedQuantity >= orderLine.Quantity)
            ? OrderStatus.Reserved
            : order.Lines.Any(orderLine => orderLine.ReservedQuantity > 0)
                ? OrderStatus.PartiallyReserved
                : OrderStatus.Confirmed;
    }

    private static void EnsureTransition(OrderStatus current, OrderStatus target)
    {
        if (current == OrderStatus.Shipped && target != OrderStatus.Shipped)
        {
            throw new InvalidOperationException("Shipped order cannot change status.");
        }

        if (target == OrderStatus.Cancelled && current == OrderStatus.Shipped)
        {
            throw new InvalidOperationException("Shipped order cannot be cancelled.");
        }

        var allowedTransitionsByStatus = new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.New] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
            [OrderStatus.Confirmed] = [OrderStatus.PartiallyReserved, OrderStatus.Reserved, OrderStatus.Cancelled],
            [OrderStatus.PartiallyReserved] = [OrderStatus.Reserved, OrderStatus.InPicking, OrderStatus.Cancelled],
            [OrderStatus.Reserved] = [OrderStatus.InPicking, OrderStatus.Cancelled],
            [OrderStatus.InPicking] = [OrderStatus.Picked, OrderStatus.Cancelled],
            [OrderStatus.Picked] = [OrderStatus.Shipped],
            [OrderStatus.Shipped] = [OrderStatus.Shipped],
            [OrderStatus.Cancelled] = [OrderStatus.Cancelled]
        };

        if (!allowedTransitionsByStatus.TryGetValue(current, out var allowedTargetStatuses) || !allowedTargetStatuses.Contains(target))
        {
            throw new InvalidOperationException($"Invalid status transition from {current} to {target}.");
        }
    }

    private static void EnsureVersion(BaseEntity entity, long expectedVersion)
    {
        if (entity.Version != expectedVersion)
        {
            throw new DbUpdateConcurrencyException("Entity has been changed by another process.");
        }
    }

    private async Task<OrderDetailsDto> GetOrderInternalAsync(int orderId, CancellationToken cancellationToken)
    {
        return await dbContext.WarehouseOrders
            .AsNoTracking()
            .Where(order => order.Id == orderId)
            .Select(ToOrderDetails())
            .FirstAsync(cancellationToken);
    }

    private async Task<PickingTaskDto> GetPickingTaskInternalAsync(int taskId, CancellationToken cancellationToken)
    {
        return await dbContext.PickingTasks
            .AsNoTracking()
            .Where(pickingTask => pickingTask.Id == taskId)
            .Select(pickingTask => new PickingTaskDto(
                pickingTask.Id,
                pickingTask.TaskNumber,
                pickingTask.Status.ToString(),
                pickingTask.CreatedAt,
                pickingTask.Lines.Select(pickingTaskLine => new PickingTaskLineDto(
                    pickingTaskLine.Id,
                    pickingTaskLine.OrderLineId,
                    pickingTaskLine.OrderLine.ItemId,
                    pickingTaskLine.OrderLine.Item.Name,
                    pickingTaskLine.Quantity,
                    pickingTaskLine.PickedQuantity)).ToList()))
            .FirstAsync(cancellationToken);
    }

    private static Expression<Func<WarehouseOrder, OrderDetailsDto>> ToOrderDetails()
    {
        return order => new OrderDetailsDto(
            order.Id,
            order.OrderNumber,
            order.CustomerId,
            order.Customer.Name,
            order.WarehouseId,
            order.Warehouse.Name,
            order.Status,
            order.CreatedAt,
            order.Version,
            order.Lines.Select(orderLine => new OrderLineDto(
                orderLine.Id,
                orderLine.ItemId,
                orderLine.Item.Name,
                orderLine.Quantity,
                orderLine.ReservedQuantity,
                orderLine.PickedQuantity)).ToList());
    }
}
