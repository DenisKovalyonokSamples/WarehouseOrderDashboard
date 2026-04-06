using Warehouse.Wpf.Models;

namespace Warehouse.Wpf.Services;

/// <summary>
/// Defines API operations used by the WPF client.
/// </summary>
public interface IWarehouseApiClient
{
    /// <summary>
    /// Returns a paged order list with optional search text.
    /// </summary>
    Task<PagedResult<OrderListItemDto>> GetOrdersAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// Returns stock overview rows.
    /// </summary>
    Task<IReadOnlyCollection<StockOverviewDto>> GetStockAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns dashboard data.
    /// </summary>
    Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates a picking task for selected order identifiers.
    /// </summary>
    Task<PickingTaskDto> CreatePickingTaskAsync(IReadOnlyCollection<int> orderIds, CancellationToken cancellationToken);

    /// <summary>
    /// Returns picking tasks for the UI.
    /// </summary>
    Task<IReadOnlyCollection<PickingTaskListItemDto>> GetPickingTasksAsync(CancellationToken cancellationToken);
}
