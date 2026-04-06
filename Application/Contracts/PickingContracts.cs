namespace Warehouse.Application.Contracts;

public sealed record CreatePickingTaskRequest(IReadOnlyCollection<int> OrderIds);

public sealed record PickingTaskLineDto(
    int Id,
    int OrderLineId,
    int ItemId,
    string ItemName,
    decimal Quantity,
    decimal PickedQuantity);

public sealed record PickingTaskDto(
    int Id,
    string TaskNumber,
    string Status,
    DateTime CreatedAt,
    IReadOnlyCollection<PickingTaskLineDto> Lines);

public sealed record CompletePickingLineRequest(decimal Quantity, long ExpectedVersion);
