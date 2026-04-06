namespace Warehouse.Wpf.Models;

public sealed class PagedResult<T>
{
    public IReadOnlyCollection<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class OrderListItemDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int LineCount { get; set; }
}

public sealed class StockOverviewDto
{
    public int ItemId { get; set; }
    public string ItemSku { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public decimal AvailableQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public bool HasShortage { get; set; }
}

public sealed class PickingTaskLineDto
{
    public int Id { get; set; }
    public int OrderLineId { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal PickedQuantity { get; set; }
}

public sealed class PickingTaskDto
{
    public int Id { get; set; }
    public string TaskNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public IReadOnlyCollection<PickingTaskLineDto> Lines { get; set; } = Array.Empty<PickingTaskLineDto>();
}

public sealed class DashboardDto
{
    public int TodayOrderCount { get; set; }
    public int OverdueTasks { get; set; }
    public int UnfulfilledOrders { get; set; }
    public double AvgMinutesFromCreateToPickingStart { get; set; }
}
