using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Features.Orders;

public sealed record OrderResponse(
    Guid Id,
    Guid CustomerId,
    OrderStatus Status,
    decimal Total,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? ShippedAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? CancelledAt,
    IReadOnlyList<OrderItemResponse> Items)
{
    public static OrderResponse FromDomain(Order order) => new(
        order.Id,
        order.CustomerId,
        order.Status,
        order.Total,
        order.CreatedAt,
        order.PaidAt,
        order.ShippedAt,
        order.DeliveredAt,
        order.CancelledAt,
        order.Items.Select(OrderItemResponse.FromDomain).ToList());
}

public sealed record OrderItemResponse(
    Guid ProductId,
    string Sku,
    string Name,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal)
{
    public static OrderItemResponse FromDomain(OrderItem item) => new(
        item.ProductId,
        item.ProductSku,
        item.ProductName,
        item.UnitPrice,
        item.Quantity,
        item.LineTotal);
}
