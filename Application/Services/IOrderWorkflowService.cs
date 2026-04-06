using Warehouse.Application.Contracts;

namespace Warehouse.Application.Services;

public interface IOrderWorkflowService
{
    Task<OrderDetailsDto> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken);
    Task<PagedResult<OrderListItemDto>> GetOrdersAsync(OrderQuery query, CancellationToken cancellationToken);
    Task<OrderDetailsDto?> GetOrderByIdAsync(int orderId, CancellationToken cancellationToken);
    Task<OrderDetailsDto> ChangeStatusAsync(int orderId, ChangeOrderStatusRequest request, CancellationToken cancellationToken);
    Task CancelOrderAsync(int orderId, long expectedVersion, CancellationToken cancellationToken);

    Task<PickingTaskDto> CreatePickingTaskAsync(CreatePickingTaskRequest request, CancellationToken cancellationToken);
    Task<PickingTaskDto> CompletePickingLineAsync(int pickingTaskLineId, CompletePickingLineRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StockOverviewDto>> GetStockOverviewAsync(int? warehouseId, CancellationToken cancellationToken);
    Task<DashboardDto> GetDashboardAsync(DateOnly day, CancellationToken cancellationToken);
}
