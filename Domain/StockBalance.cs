namespace Warehouse.Domain;

public sealed class StockBalance : BaseEntity
{
    public int ItemId { get; set; }
    public int WarehouseId { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }

    public Item Item { get; set; } = null!;
    public WarehouseLocation Warehouse { get; set; } = null!;
}
