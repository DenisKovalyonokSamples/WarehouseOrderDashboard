namespace Warehouse.Domain;

public sealed class WarehouseOrder : BaseEntity
{
    public string OrderNumber { get; set; }
    public string Status { get; set; }
}
