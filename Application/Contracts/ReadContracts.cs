namespace Warehouse.Application.Contracts;

/// <summary>
/// Represents a paged query result.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int TotalCount);

/// <summary>
/// Represents stock overview data for an item in a warehouse.
/// </summary>
public sealed record StockOverviewDto(
    int ItemId,
    string ItemSku,
    string ItemName,
    int WarehouseId,
    string WarehouseName,
    decimal AvailableQuantity,
    decimal ReservedQuantity,
    bool HasShortage);

/// <summary>
/// Represents dashboard metrics for the selected day.
/// </summary>
public sealed record DashboardDto(
    int TodayOrderCount,
    int OverdueTasks,
    int UnfulfilledOrders,
    double AvgMinutesFromCreateToPickingStart);
