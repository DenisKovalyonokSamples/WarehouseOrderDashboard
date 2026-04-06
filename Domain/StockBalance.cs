namespace Warehouse.Domain;

/// <summary>
/// Represents current item stock and reserved quantity for a warehouse.
/// </summary>
public sealed class StockBalance : BaseEntity
{
    public int ItemId { get; set; }
    public int WarehouseId { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }

    public Item Item { get; set; } = null!;
    public WarehouseLocation Warehouse { get; set; } = null!;
}
