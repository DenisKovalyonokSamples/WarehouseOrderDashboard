namespace Warehouse.Domain;

public sealed class PickingTaskLine : BaseEntity
{
    public int PickingTaskId { get; set; }
    public int OrderLineId { get; set; }
    public decimal Quantity { get; set; }
    public decimal PickedQuantity { get; set; }

    public PickingTask PickingTask { get; set; } = null!;
    public OrderLine OrderLine { get; set; } = null!;
}
