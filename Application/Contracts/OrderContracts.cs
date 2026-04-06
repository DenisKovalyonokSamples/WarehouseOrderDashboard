using Warehouse.Domain;

namespace Warehouse.Application.Contracts;

public sealed record OrderLineCreateDto(int ItemId, decimal Quantity);

public sealed record CreateOrderRequest(
    string OrderNumber,
    int CustomerId,
    int WarehouseId,
    IReadOnlyCollection<OrderLineCreateDto> Lines);

public sealed record OrderLineDto(
    int Id,
    int ItemId,
    string ItemName,
    decimal Quantity,
    decimal ReservedQuantity,
    decimal PickedQuantity);

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

public sealed class OrderQuery
{
    public DateTime? CreatedFromUtc { get; init; }
    public DateTime? CreatedToUtc { get; init; }
    public OrderStatus? Status { get; init; }
    public int? CustomerId { get; init; }
    public int? WarehouseId { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed record ChangeOrderStatusRequest(OrderStatus TargetStatus, long ExpectedVersion);
