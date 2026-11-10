using OrderFlow.Domain.Common;

namespace OrderFlow.Domain.Orders;

public static class OrderErrors
{
    public static readonly Error Empty =
        Error.Validation("Orders.Empty", "An order must contain at least one item.");

    public static readonly Error DuplicateProduct =
        Error.Validation("Orders.DuplicateProduct", "Each product may appear only once per order.");

    public static Error TooManyLines(int max) =>
        Error.Validation("Orders.TooManyLines", $"An order cannot contain more than {max} items.");

    public static Error NotFound(Guid orderId) =>
        Error.NotFound("Orders.NotFound", $"Order '{orderId}' was not found.");

    public static Error InvalidTransition(OrderStatus from, OrderStatus to) =>
        Error.Conflict("Orders.InvalidStatusTransition", $"An order cannot move from '{from}' to '{to}'.");
}
