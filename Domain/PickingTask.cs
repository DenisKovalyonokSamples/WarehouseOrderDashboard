namespace Warehouse.Domain;

public sealed class PickingTask : BaseEntity
{
    public string TaskNumber { get; set; } = string.Empty;
    public PickingTaskStatus Status { get; set; } = PickingTaskStatus.New;

    public ICollection<PickingTaskLine> Lines { get; set; } = new List<PickingTaskLine>();
}
