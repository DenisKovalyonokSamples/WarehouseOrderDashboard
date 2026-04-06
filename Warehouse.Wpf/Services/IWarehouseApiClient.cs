using Warehouse.Wpf.Models;

namespace Warehouse.Wpf.Services;

public interface IWarehouseApiClient
{
    Task<PagedResult<OrderListItemDto>> GetOrdersAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<StockOverviewDto>> GetStockAsync(CancellationToken cancellationToken);
    Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken);
    Task<PickingTaskDto> CreatePickingTaskAsync(IReadOnlyCollection<int> orderIds, CancellationToken cancellationToken);
}
