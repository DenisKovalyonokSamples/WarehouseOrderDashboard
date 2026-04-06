namespace Warehouse.Domain;

/// <summary>
/// Represents reserved stock linked to a specific order line.
/// </summary>
public sealed class StockReservation : BaseEntity
{
    public int OrderLineId { get; set; }
    public int WarehouseId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }

    public OrderLine OrderLine { get; set; } = null!;
    public WarehouseLocation Warehouse { get; set; } = null!;
    public Item Item { get; set; } = null!;
}
