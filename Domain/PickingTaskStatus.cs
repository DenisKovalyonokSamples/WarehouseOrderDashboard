namespace Warehouse.Domain;

/// <summary>
/// Defines lifecycle states for a picking task.
/// </summary>
public enum PickingTaskStatus
{
    New = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}
