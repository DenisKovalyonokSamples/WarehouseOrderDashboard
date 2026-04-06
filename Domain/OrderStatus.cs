namespace Warehouse.Domain;

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
