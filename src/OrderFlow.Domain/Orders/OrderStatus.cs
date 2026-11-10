namespace OrderFlow.Domain.Orders;

/// <summary>
/// Lifecycle of an order:
/// <c>Pending → Paid → Shipped → Delivered</c>, and <c>Pending | Paid → Cancelled</c>.
/// </summary>
public enum OrderStatus
{
    Pending = 1,
    Paid = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5,
}
