namespace Warehouse.Domain;

/// <summary>
/// Represents a physical warehouse location.
/// </summary>
public sealed class WarehouseLocation : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public ICollection<WarehouseOrder> Orders { get; set; } = new List<WarehouseOrder>();
    public ICollection<StockBalance> StockBalances { get; set; } = new List<StockBalance>();
}
