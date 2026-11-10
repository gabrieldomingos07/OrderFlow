using OrderFlow.Domain.Common;
using OrderFlow.Domain.Products;

namespace OrderFlow.Domain.Orders;

/// <summary>
/// A line of an order. Product data (SKU, name and price) is copied at the moment the order is
/// placed, so later catalog changes never rewrite the history of what the customer bought.
/// </summary>
public sealed class OrderItem : Entity
{
    internal OrderItem(Guid id, Guid orderId, Product product, int quantity)
        : base(id)
    {
        OrderId = orderId;
        ProductId = product.Id;
        ProductSku = product.Sku;
        ProductName = product.Name;
        UnitPrice = product.Price;
        Quantity = quantity;
    }

    private OrderItem()
    {
    }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductSku { get; private set; } = string.Empty;

    public string ProductName { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;
}
