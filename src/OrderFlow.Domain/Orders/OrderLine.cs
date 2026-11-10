using OrderFlow.Domain.Products;

namespace OrderFlow.Domain.Orders;

/// <summary>A requested product and quantity used to place an <see cref="Order"/>.</summary>
public sealed record OrderLine(Product Product, int Quantity);
