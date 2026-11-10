using OrderFlow.Domain.Products;
using OrderFlow.Domain.Users;

namespace OrderFlow.Application.UnitTests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static Product NewProduct(decimal price = 100m, int stock = 10, string sku = "SKU-1") =>
        Product.Create(sku, "Product " + sku, null, price, stock, Now).Value;

    public static User NewUser(string email = "jane@example.com", UserRole role = UserRole.Customer) =>
        User.Create(email, "Jane Doe", "hashed-password", role, Now);
}
