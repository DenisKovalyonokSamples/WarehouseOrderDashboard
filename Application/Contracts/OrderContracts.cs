using Warehouse.Domain;

namespace Warehouse.Application.Contracts;

/// <summary>
/// Represents a line request used to create an order.
/// </summary>
public sealed record OrderLineCreateDto(int ItemId, decimal Quantity);

/// <summary>
/// Represents request data to create a warehouse order.
/// </summary>
public sealed record CreateOrderRequest(
    string OrderNumber,
    int CustomerId,
    int WarehouseId,
    IReadOnlyCollection<OrderLineCreateDto> Lines);

/// <summary>
/// Represents line details in order responses.
/// </summary>
public sealed record OrderLineDto(
    int Id,
    int ItemId,
    string ItemName,
    decimal Quantity,
    decimal ReservedQuantity,
    decimal PickedQuantity);

/// <summary>
/// Represents a summarized order row for list views.
/// </summary>
public sealed record OrderListItemDto(
    int Id,
    string OrderNumber,
    int CustomerId,
    string CustomerName,
    int WarehouseId,
    string WarehouseName,
    OrderStatus Status,
    DateTime CreatedAt,
    int LineCount);

/// <summary>
/// Represents full order details.
/// </summary>
public sealed record OrderDetailsDto(
    int Id,
    string OrderNumber,
    int CustomerId,
    string CustomerName,
    int WarehouseId,
    string WarehouseName,
    OrderStatus Status,
    DateTime CreatedAt,
    long Version,
    IReadOnlyCollection<OrderLineDto> Lines);

/// <summary>
/// Represents filters and paging options for order queries.
/// </summary>
public sealed class OrderQuery
{
    public DateTime? CreatedFromUtc { get; init; }
    public DateTime? CreatedToUtc { get; init; }
    public OrderStatus? Status { get; init; }
    public int? CustomerId { get; init; }
    public int? WarehouseId { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 29;
}

/// <summary>
/// Represents a request to change order status with expected version.
/// </summary>
public sealed record ChangeOrderStatusRequest(OrderStatus TargetStatus, long ExpectedVersion);
