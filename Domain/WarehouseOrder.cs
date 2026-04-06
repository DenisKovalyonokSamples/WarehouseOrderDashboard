namespace Warehouse.Domain;

public sealed class WarehouseOrder : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public int WarehouseId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.New;
    public DateTime? PickingStartedAt { get; set; }
    public DateTime? ShippedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public WarehouseLocation Warehouse { get; set; } = null!;
    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();
}
