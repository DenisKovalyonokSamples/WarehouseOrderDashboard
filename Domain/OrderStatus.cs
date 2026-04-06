namespace Warehouse.Domain;

/// <summary>
/// Defines lifecycle states for a warehouse order.
/// </summary>
public enum OrderStatus
{
    New = 0,
    Confirmed = 1,
    PartiallyReserved = 2,
    Reserved = 3,
    InPicking = 4,
    Picked = 5,
    Shipped = 6,
    Cancelled = 7
}
