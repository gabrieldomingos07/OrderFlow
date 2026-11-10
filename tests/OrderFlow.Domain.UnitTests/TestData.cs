using OrderFlow.Domain.Products;

namespace OrderFlow.Domain.UnitTests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    public static Product NewProduct(decimal price = 100m, int stock = 10, string sku = "SKU-1", string name = "Keyboard") =>
        Domain.Products.Product.Create(sku, name, "A product", price, stock, Now).Value;
}
