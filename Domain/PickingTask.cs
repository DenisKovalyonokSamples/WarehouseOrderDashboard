namespace Warehouse.Domain;

/// <summary>
/// Represents a warehouse picking task grouped from one or more orders.
/// </summary>
public sealed class PickingTask : BaseEntity
{
    public string TaskNumber { get; set; } = string.Empty;
    public PickingTaskStatus Status { get; set; } = PickingTaskStatus.New;

    public ICollection<PickingTaskLine> Lines { get; set; } = new List<PickingTaskLine>();
}
