using Warehouse.Application.Contracts;

namespace Warehouse.Application.Services;

/// <summary>
/// Defines application operations for order lifecycle, picking, stock overview, and dashboard data.
/// </summary>
public interface IOrderWorkflowService
{
    /// <summary>
    /// Creates a new order.
    /// </summary>
    Task<OrderDetailsDto> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Returns paged order list using query filters.
    /// </summary>
    Task<PagedResult<OrderListItemDto>> GetOrdersAsync(OrderQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Returns order details by identifier.
    /// </summary>
    Task<OrderDetailsDto?> GetOrderByIdAsync(int orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Changes order status using optimistic concurrency validation.
    /// </summary>
    Task<OrderDetailsDto> ChangeStatusAsync(int orderId, ChangeOrderStatusRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Cancels an order using expected entity version.
    /// </summary>
    Task CancelOrderAsync(int orderId, long expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a picking task from selected order identifiers.
    /// </summary>
    Task<PickingTaskDto> CreatePickingTaskAsync(CreatePickingTaskRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Completes quantity for a picking task line.
    /// </summary>
    Task<PickingTaskDto> CompletePickingLineAsync(int pickingTaskLineId, CompletePickingLineRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Returns stock overview, optionally filtered by warehouse.
    /// </summary>
    Task<IReadOnlyCollection<StockOverviewDto>> GetStockOverviewAsync(int? warehouseId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns dashboard metrics for the specified day.
    /// </summary>
    Task<DashboardDto> GetDashboardAsync(DateOnly day, CancellationToken cancellationToken);
}
