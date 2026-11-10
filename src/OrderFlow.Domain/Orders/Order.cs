using OrderFlow.Domain.Common;
using OrderFlow.Domain.Products;

namespace OrderFlow.Domain.Orders;

/// <summary>
/// Aggregate root for a customer order. All state transitions go through methods that enforce
/// the lifecycle described in <see cref="OrderStatus"/>.
/// </summary>
public sealed class Order : Entity
{
    public const int MaxLines = 50;

    private readonly List<OrderItem> _items = [];

    private Order(Guid id, Guid customerId, DateTimeOffset createdAt)
        : base(id)
    {
        CustomerId = customerId;
        Status = OrderStatus.Pending;
        CreatedAt = createdAt;
    }

    private Order()
    {
    }

    public Guid CustomerId { get; private set; }

    public OrderStatus Status { get; private set; }

    public decimal Total { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public DateTimeOffset? ShippedAt { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Places a new order and reserves stock for every line. Either every line is reserved or
    /// nothing changes: all lines are validated before any product is touched.
    /// </summary>
    public static Result<Order> Place(Guid customerId, IReadOnlyCollection<OrderLine> lines, DateTimeOffset now)
    {
        if (lines.Count == 0)
        {
            return OrderErrors.Empty;
        }

        if (lines.Count > MaxLines)
        {
            return OrderErrors.TooManyLines(MaxLines);
        }

        if (lines.DistinctBy(line => line.Product.Id).Count() != lines.Count)
        {
            return OrderErrors.DuplicateProduct;
        }

        foreach (var line in lines)
        {
            var check = line.Product.EnsureCanReserve(line.Quantity);
            if (check.IsFailure)
            {
                return check.Error;
            }
        }

        var order = new Order(Guid.CreateVersion7(now), customerId, now);
        foreach (var line in lines)
        {
            var reservation = line.Product.Reserve(line.Quantity, now);
            if (reservation.IsFailure)
            {
                // Unreachable: every line was validated above.
                throw new InvalidOperationException(reservation.Error.Description);
            }

            order._items.Add(new OrderItem(Guid.CreateVersion7(now), order.Id, line.Product, line.Quantity));
        }

        order.Total = order._items.Sum(item => item.LineTotal);
        return order;
    }

    public Result MarkAsPaid(DateTimeOffset now)
    {
        if (Status != OrderStatus.Pending)
        {
            return OrderErrors.InvalidTransition(Status, OrderStatus.Paid);
        }

        Status = OrderStatus.Paid;
        PaidAt = now;
        return Result.Success();
    }

    public Result Ship(DateTimeOffset now)
    {
        if (Status != OrderStatus.Paid)
        {
            return OrderErrors.InvalidTransition(Status, OrderStatus.Shipped);
        }

        Status = OrderStatus.Shipped;
        ShippedAt = now;
        return Result.Success();
    }

    public Result Deliver(DateTimeOffset now)
    {
        if (Status != OrderStatus.Shipped)
        {
            return OrderErrors.InvalidTransition(Status, OrderStatus.Delivered);
        }

        Status = OrderStatus.Delivered;
        DeliveredAt = now;
        return Result.Success();
    }

    /// <summary>
    /// Cancels the order and gives the reserved units back to stock.
    /// </summary>
    /// <param name="products">The products referenced by this order, indexed by id.</param>
    /// <param name="now">The current time.</param>
    public Result Cancel(IReadOnlyDictionary<Guid, Product> products, DateTimeOffset now)
    {
        if (Status is not (OrderStatus.Pending or OrderStatus.Paid))
        {
            return OrderErrors.InvalidTransition(Status, OrderStatus.Cancelled);
        }

        var missing = _items.FirstOrDefault(item => !products.ContainsKey(item.ProductId));
        if (missing is not null)
        {
            throw new ArgumentException(
                $"Product '{missing.ProductId}' is required to cancel order '{Id}'.", nameof(products));
        }

        foreach (var item in _items)
        {
            var release = products[item.ProductId].Release(item.Quantity, now);
            if (release.IsFailure)
            {
                return release.Error;
            }
        }

        Status = OrderStatus.Cancelled;
        CancelledAt = now;
        return Result.Success();
    }

    public bool IsOwnedBy(Guid customerId) => CustomerId == customerId;
}
