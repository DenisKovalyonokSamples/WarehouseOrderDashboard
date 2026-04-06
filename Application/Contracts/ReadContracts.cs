namespace Warehouse.Application.Contracts;

public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int TotalCount);

public sealed record StockOverviewDto(
    int ItemId,
    string ItemSku,
    string ItemName,
    int WarehouseId,
    string WarehouseName,
    decimal AvailableQuantity,
    decimal ReservedQuantity,
    bool HasShortage);

public sealed record DashboardDto(
    int TodayOrderCount,
    int OverdueTasks,
    int UnfulfilledOrders,
    double AvgMinutesFromCreateToPickingStart);
