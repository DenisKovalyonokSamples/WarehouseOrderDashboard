using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Linq.Expressions;
using Warehouse.Application.Contracts;
using Warehouse.Application.Services;
using Warehouse.Domain;

namespace Warehouse.Infrastructure.Services;

public sealed class OrderWorkflowService(AppDbContext dbContext) : IOrderWorkflowService
{
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

    public async Task<PagedResult<OrderListItemDto>> GetOrdersAsync(OrderQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var ordersQuery = dbContext.WarehouseOrders
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Warehouse)
            .Include(x => x.Lines)
            .AsQueryable();

        if (query.CreatedFromUtc.HasValue)
        {
            ordersQuery = ordersQuery.Where(x => x.CreatedAt >= query.CreatedFromUtc.Value);
        }

        if (query.CreatedToUtc.HasValue)
        {
            ordersQuery = ordersQuery.Where(x => x.CreatedAt <= query.CreatedToUtc.Value);
        }

        if (query.Status.HasValue)
        {
            ordersQuery = ordersQuery.Where(x => x.Status == query.Status.Value);
        }

        if (query.CustomerId.HasValue)
        {
            ordersQuery = ordersQuery.Where(x => x.CustomerId == query.CustomerId.Value);
        }

        if (query.WarehouseId.HasValue)
        {
            ordersQuery = ordersQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            ordersQuery = ordersQuery.Where(x => x.OrderNumber.Contains(search) || x.Customer.Name.Contains(search));
        }

        var totalCount = await ordersQuery.CountAsync(cancellationToken);

        var items = await ordersQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new OrderListItemDto(
                x.Id,
                x.OrderNumber,
                x.CustomerId,
                x.Customer.Name,
                x.WarehouseId,
                x.Warehouse.Name,
                x.Status,
                x.CreatedAt,
                x.Lines.Count))
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderListItemDto>(items, page, pageSize, totalCount);
    }

    public async Task<OrderDetailsDto?> GetOrderByIdAsync(int orderId, CancellationToken cancellationToken)
    {
        return await dbContext.WarehouseOrders
            .AsNoTracking()
            .Where(x => x.Id == orderId)
            .Select(ToOrderDetails())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<OrderDetailsDto> ChangeStatusAsync(int orderId, ChangeOrderStatusRequest request, CancellationToken cancellationToken)
    {
        var tx = await BeginTransactionIfRelationalAsync(cancellationToken);

        var order = await dbContext.WarehouseOrders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken)
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
        if (tx is not null)
        {
            await tx.CommitAsync(cancellationToken);
            await tx.DisposeAsync();
        }

        return await GetOrderInternalAsync(orderId, cancellationToken);
    }

    public async Task CancelOrderAsync(int orderId, long expectedVersion, CancellationToken cancellationToken)
    {
        var result = await ChangeStatusAsync(orderId, new ChangeOrderStatusRequest(OrderStatus.Cancelled, expectedVersion), cancellationToken);
        _ = result;
    }

    public async Task<PickingTaskDto> CreatePickingTaskAsync(CreatePickingTaskRequest request, CancellationToken cancellationToken)
    {
        if (request.OrderIds.Count == 0)
        {
            throw new InvalidOperationException("At least one order is required.");
        }

        var tx = await BeginTransactionIfRelationalAsync(cancellationToken);

        var orders = await dbContext.WarehouseOrders
            .Include(x => x.Lines)
            .Where(x => request.OrderIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (orders.Count != request.OrderIds.Count)
        {
            throw new KeyNotFoundException("One or more orders not found.");
        }

        var pickingTask = new PickingTask
        {
            TaskNumber = $"PT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
            Status = PickingTaskStatus.New
        };

        foreach (var order in orders)
        {
            if (order.Status is not (OrderStatus.Reserved or OrderStatus.PartiallyReserved or OrderStatus.Confirmed))
            {
                throw new InvalidOperationException($"Order {order.OrderNumber} is not in a pickable status.");
            }

            foreach (var line in order.Lines.Where(x => x.ReservedQuantity - x.PickedQuantity > 0))
            {
                pickingTask.Lines.Add(new PickingTaskLine
                {
                    OrderLineId = line.Id,
                    Quantity = line.ReservedQuantity - line.PickedQuantity
                });
            }

            order.Status = OrderStatus.InPicking;
            order.PickingStartedAt ??= DateTime.UtcNow;
        }

        if (pickingTask.Lines.Count == 0)
        {
            throw new InvalidOperationException("No reservable lines found for picking task.");
        }

        dbContext.PickingTasks.Add(pickingTask);
        dbContext.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(PickingTask),
            Action = "Created",
            Details = $"Task {pickingTask.TaskNumber} created for {orders.Count} orders."
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null)
        {
            await tx.CommitAsync(cancellationToken);
            await tx.DisposeAsync();
        }

        return await GetPickingTaskInternalAsync(pickingTask.Id, cancellationToken);
    }

    public async Task<PickingTaskDto> CompletePickingLineAsync(int pickingTaskLineId, CompletePickingLineRequest request, CancellationToken cancellationToken)
    {
        var tx = await BeginTransactionIfRelationalAsync(cancellationToken);

        var line = await dbContext.PickingTaskLines
            .Include(x => x.PickingTask)
            .ThenInclude(x => x.Lines)
            .Include(x => x.OrderLine)
            .ThenInclude(x => x.Order)
            .ThenInclude(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == pickingTaskLineId, cancellationToken)
            ?? throw new KeyNotFoundException("Picking line not found.");

        EnsureVersion(line, request.ExpectedVersion);

        var remaining = line.Quantity - line.PickedQuantity;
        if (request.Quantity <= 0 || request.Quantity > remaining)
        {
            throw new InvalidOperationException("Invalid picked quantity.");
        }

        var stock = await dbContext.StockBalances
            .FirstOrDefaultAsync(x => x.ItemId == line.OrderLine.ItemId && x.WarehouseId == line.OrderLine.Order.WarehouseId, cancellationToken)
            ?? throw new KeyNotFoundException("Stock record not found.");

        line.PickedQuantity += request.Quantity;
        line.OrderLine.PickedQuantity += request.Quantity;

        stock.ReservedQuantity = Math.Max(0, stock.ReservedQuantity - request.Quantity);
        stock.AvailableQuantity = Math.Max(0, stock.AvailableQuantity - request.Quantity);

        line.PickingTask.Status = line.PickingTask.Lines.All(x => x.PickedQuantity >= x.Quantity)
            ? PickingTaskStatus.Completed
            : PickingTaskStatus.InProgress;

        if (line.OrderLine.Order.Lines.All(x => x.PickedQuantity >= x.Quantity))
        {
            line.OrderLine.Order.Status = OrderStatus.Picked;
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(PickingTaskLine),
            EntityId = line.Id,
            Action = "Picked",
            Details = $"Picked quantity {request.Quantity}."
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        if (tx is not null)
        {
            await tx.CommitAsync(cancellationToken);
            await tx.DisposeAsync();
        }

        return await GetPickingTaskInternalAsync(line.PickingTaskId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<StockOverviewDto>> GetStockOverviewAsync(int? warehouseId, CancellationToken cancellationToken)
    {
        var query = dbContext.StockBalances
            .AsNoTracking()
            .Include(x => x.Item)
            .Include(x => x.Warehouse)
            .AsQueryable();

        if (warehouseId.HasValue)
        {
            query = query.Where(x => x.WarehouseId == warehouseId);
        }

        return await query
            .OrderBy(x => x.Item.Sku)
            .Select(x => new StockOverviewDto(
                x.ItemId,
                x.Item.Sku,
                x.Item.Name,
                x.WarehouseId,
                x.Warehouse.Name,
                x.AvailableQuantity,
                x.ReservedQuantity,
                x.AvailableQuantity - x.ReservedQuantity < 0))
            .ToListAsync(cancellationToken);
    }

    public async Task<DashboardDto> GetDashboardAsync(DateOnly day, CancellationToken cancellationToken)
    {
        var start = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var todayOrderCount = await dbContext.WarehouseOrders.AsNoTracking().CountAsync(x => x.CreatedAt >= start && x.CreatedAt < end, cancellationToken);
        var overdueTasks = await dbContext.PickingTasks.AsNoTracking().CountAsync(x => x.Status != PickingTaskStatus.Completed && x.CreatedAt < DateTime.UtcNow.AddHours(-4), cancellationToken);
        var unfulfilledOrders = await dbContext.WarehouseOrders.AsNoTracking().CountAsync(x => x.Status != OrderStatus.Shipped && x.Status != OrderStatus.Cancelled, cancellationToken);

        var pickingCycleDurations = await dbContext.WarehouseOrders
            .AsNoTracking()
            .Where(x => x.PickingStartedAt.HasValue)
            .Select(x => new { x.CreatedAt, x.PickingStartedAt })
            .ToListAsync(cancellationToken);

        var avgMinutes = pickingCycleDurations.Count == 0
            ? 0
            : pickingCycleDurations.Average(x => (x.PickingStartedAt!.Value - x.CreatedAt).TotalMinutes);

        return new DashboardDto(todayOrderCount, overdueTasks, unfulfilledOrders, avgMinutes);
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfRelationalAsync(CancellationToken cancellationToken)
    {
        return dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
    }

    private async Task ReserveAsync(WarehouseOrder order, CancellationToken cancellationToken)
    {
        foreach (var line in order.Lines)
        {
            var toReserve = line.Quantity - line.ReservedQuantity;
            if (toReserve <= 0)
            {
                continue;
            }

            var stock = await dbContext.StockBalances
                .FirstOrDefaultAsync(x => x.ItemId == line.ItemId && x.WarehouseId == order.WarehouseId, cancellationToken)
                ?? throw new InvalidOperationException($"Stock not found for item {line.ItemId} and warehouse {order.WarehouseId}.");

            var free = Math.Max(0, stock.AvailableQuantity - stock.ReservedQuantity);
            var reservable = Math.Min(free, toReserve);

            if (reservable <= 0)
            {
                continue;
            }

            stock.ReservedQuantity += reservable;
            line.ReservedQuantity += reservable;

            dbContext.StockReservations.Add(new StockReservation
            {
                OrderLineId = line.Id,
                ItemId = line.ItemId,
                WarehouseId = order.WarehouseId,
                Quantity = reservable
            });
        }

        order.Status = order.Lines.All(x => x.ReservedQuantity >= x.Quantity)
            ? OrderStatus.Reserved
            : order.Lines.Any(x => x.ReservedQuantity > 0)
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

        var transitions = new Dictionary<OrderStatus, OrderStatus[]>
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

        if (!transitions.TryGetValue(current, out var allowed) || !allowed.Contains(target))
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
            .Where(x => x.Id == orderId)
            .Select(ToOrderDetails())
            .FirstAsync(cancellationToken);
    }

    private async Task<PickingTaskDto> GetPickingTaskInternalAsync(int taskId, CancellationToken cancellationToken)
    {
        return await dbContext.PickingTasks
            .AsNoTracking()
            .Where(x => x.Id == taskId)
            .Select(x => new PickingTaskDto(
                x.Id,
                x.TaskNumber,
                x.Status.ToString(),
                x.CreatedAt,
                x.Lines.Select(l => new PickingTaskLineDto(
                    l.Id,
                    l.OrderLineId,
                    l.OrderLine.ItemId,
                    l.OrderLine.Item.Name,
                    l.Quantity,
                    l.PickedQuantity)).ToList()))
            .FirstAsync(cancellationToken);
    }

    private static Expression<Func<WarehouseOrder, OrderDetailsDto>> ToOrderDetails()
    {
        return x => new OrderDetailsDto(
            x.Id,
            x.OrderNumber,
            x.CustomerId,
            x.Customer.Name,
            x.WarehouseId,
            x.Warehouse.Name,
            x.Status,
            x.CreatedAt,
            x.Version,
            x.Lines.Select(l => new OrderLineDto(
                l.Id,
                l.ItemId,
                l.Item.Name,
                l.Quantity,
                l.ReservedQuantity,
                l.PickedQuantity)).ToList());
    }
}
