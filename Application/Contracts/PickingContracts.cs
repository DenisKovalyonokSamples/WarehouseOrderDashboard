namespace Warehouse.Application.Contracts;

/// <summary>
/// Represents request data to create a picking task.
/// </summary>
public sealed record CreatePickingTaskRequest(IReadOnlyCollection<int> OrderIds);

/// <summary>
/// Represents line details for a picking task.
/// </summary>
public sealed record PickingTaskLineDto(
    int Id,
    int OrderLineId,
    int ItemId,
    string ItemName,
    decimal Quantity,
    decimal PickedQuantity);

/// <summary>
/// Represents picking task details.
/// </summary>
public sealed record PickingTaskDto(
    int Id,
    string TaskNumber,
    string Status,
    DateTime CreatedAt,
    IReadOnlyCollection<PickingTaskLineDto> Lines);

/// <summary>
/// Represents picking task list data.
/// </summary>
public sealed record PickingTaskListItemDto(
    int Id,
    string TaskNumber,
    string Status,
    DateTime CreatedAt,
    int LineCount,
    decimal TotalQuantity,
    decimal PickedQuantity);

/// <summary>
/// Represents request data for completing a picking task line.
/// </summary>
public sealed record CompletePickingLineRequest(decimal Quantity, long ExpectedVersion);
