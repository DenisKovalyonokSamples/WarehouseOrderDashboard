namespace Warehouse.Domain;

/// <summary>
/// Represents a catalog item that can be ordered and stocked.
/// </summary>
public sealed class Item : BaseEntity
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public ICollection<OrderLine> OrderLines { get; set; } = new List<OrderLine>();
    public ICollection<StockBalance> StockBalances { get; set; } = new List<StockBalance>();
}
