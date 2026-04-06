namespace Warehouse.Domain;

/// <summary>
/// Represents a customer who places warehouse orders.
/// </summary>
public sealed class Customer : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public ICollection<WarehouseOrder> Orders { get; set; } = new List<WarehouseOrder>();
}
