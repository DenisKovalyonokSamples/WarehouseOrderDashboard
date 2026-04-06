namespace Warehouse.Domain;

public sealed class OrderLine : BaseEntity
{
    public int OrderId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal PickedQuantity { get; set; }

    public WarehouseOrder Order { get; set; } = null!;
    public Item Item { get; set; } = null!;
    public ICollection<StockReservation> Reservations { get; set; } = new List<StockReservation>();
    public ICollection<PickingTaskLine> PickingTaskLines { get; set; } = new List<PickingTaskLine>();
}
