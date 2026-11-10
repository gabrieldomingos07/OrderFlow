using OrderFlow.Domain.Products;
using static OrderFlow.Domain.UnitTests.TestData;

namespace OrderFlow.Domain.UnitTests.Products;

public sealed class ProductTests
{
    [Fact]
    public void Create_returns_an_active_product_with_normalized_data()
    {
        var result = Domain.Products.Product.Create("  kb-01 ", "  Keyboard ", "   ", 199.90m, 5, Now);

        result.IsSuccess.ShouldBeTrue();
        var product = result.Value;
        product.Sku.ShouldBe("KB-01");
        product.Name.ShouldBe("Keyboard");
        product.Description.ShouldBeNull();
        product.Price.ShouldBe(199.90m);
        product.StockQuantity.ShouldBe(5);
        product.IsActive.ShouldBeTrue();
        product.CreatedAt.ShouldBe(Now);
        product.Id.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(1_000_000.01)]
    public void Create_rejects_a_price_out_of_range(decimal price)
    {
        var result = Domain.Products.Product.Create("KB-01", "Keyboard", null, price, 5, Now);

        result.Error.ShouldBe(ProductErrors.InvalidPrice);
    }

    [Fact]
    public void Create_accepts_the_maximum_price()
    {
        Domain.Products.Product.Create("KB-01", "Keyboard", null, Domain.Products.Product.MaxPrice, 5, Now)
            .IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_rejects_negative_stock()
    {
        var result = Domain.Products.Product.Create("KB-01", "Keyboard", null, 10m, -1, Now);

        result.Error.ShouldBe(ProductErrors.NegativeStock);
    }

    [Theory]
    [InlineData("", "Keyboard")]
    [InlineData("KB-01", " ")]
    public void Create_requires_sku_and_name(string sku, string name)
    {
        var result = Domain.Products.Product.Create(sku, name, null, 10m, 1, Now);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Reserve_takes_units_out_of_stock()
    {
        var product = NewProduct(stock: 10);

        var result = product.Reserve(3, Now.AddMinutes(1));

        result.IsSuccess.ShouldBeTrue();
        product.StockQuantity.ShouldBe(7);
        product.UpdatedAt.ShouldBe(Now.AddMinutes(1));
    }

    [Fact]
    public void Reserve_more_than_available_fails_and_keeps_stock()
    {
        var product = NewProduct(stock: 2);

        var result = product.Reserve(3, Now);

        result.Error.Code.ShouldBe("Products.InsufficientStock");
        product.StockQuantity.ShouldBe(2);
    }

    [Fact]
    public void Reserve_on_an_inactive_product_fails()
    {
        var product = NewProduct(stock: 10);
        product.Deactivate(Now);

        var result = product.Reserve(1, Now);

        result.Error.Code.ShouldBe("Products.Inactive");
        product.StockQuantity.ShouldBe(10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Stock_movements_require_a_positive_quantity(int quantity)
    {
        var product = NewProduct(stock: 10);

        product.Reserve(quantity, Now).Error.ShouldBe(ProductErrors.InvalidQuantity);
        product.Restock(quantity, Now).Error.ShouldBe(ProductErrors.InvalidQuantity);
        product.Release(quantity, Now).Error.ShouldBe(ProductErrors.InvalidQuantity);
        product.StockQuantity.ShouldBe(10);
    }

    [Fact]
    public void Restock_adds_units()
    {
        var product = NewProduct(stock: 1);

        product.Restock(9, Now).IsSuccess.ShouldBeTrue();

        product.StockQuantity.ShouldBe(10);
    }

    [Fact]
    public void Restock_rejects_overflowing_the_stock()
    {
        var product = NewProduct(stock: 10);

        var result = product.Restock(int.MaxValue, Now);

        result.Error.ShouldBe(ProductErrors.StockLimitExceeded);
        product.StockQuantity.ShouldBe(10);
    }

    [Fact]
    public void Update_changes_the_details()
    {
        var product = NewProduct(price: 10m);

        var result = product.Update(" New name ", " New description ", 25.50m, Now.AddDays(1));

        result.IsSuccess.ShouldBeTrue();
        product.Name.ShouldBe("New name");
        product.Description.ShouldBe("New description");
        product.Price.ShouldBe(25.50m);
        product.UpdatedAt.ShouldBe(Now.AddDays(1));
    }

    [Fact]
    public void Update_rejects_an_invalid_price_and_keeps_the_old_one()
    {
        var product = NewProduct(price: 10m);

        var result = product.Update("Name", null, 0m, Now);

        result.Error.ShouldBe(ProductErrors.InvalidPrice);
        product.Price.ShouldBe(10m);
    }

    [Fact]
    public void Deactivate_and_activate_toggle_availability()
    {
        var product = NewProduct();

        product.Deactivate(Now);
        product.IsActive.ShouldBeFalse();

        product.Activate(Now);
        product.IsActive.ShouldBeTrue();
    }
}
