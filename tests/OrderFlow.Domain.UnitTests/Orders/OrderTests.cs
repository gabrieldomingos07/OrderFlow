using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Products;
using static OrderFlow.Domain.UnitTests.TestData;

namespace OrderFlow.Domain.UnitTests.Orders;

public sealed class OrderTests
{
    private static readonly Guid CustomerId = Guid.CreateVersion7();

    [Fact]
    public void Place_creates_a_pending_order_and_reserves_stock()
    {
        var keyboard = NewProduct(price: 100m, stock: 10, sku: "KB");
        var mouse = NewProduct(price: 25.50m, stock: 5, sku: "MS");

        var result = Order.Place(CustomerId, [new(keyboard, 2), new(mouse, 3)], Now);

        result.IsSuccess.ShouldBeTrue();
        var order = result.Value;
        order.Status.ShouldBe(OrderStatus.Pending);
        order.CustomerId.ShouldBe(CustomerId);
        order.Items.Count.ShouldBe(2);
        order.Total.ShouldBe(276.50m);
        keyboard.StockQuantity.ShouldBe(8);
        mouse.StockQuantity.ShouldBe(2);
    }

    [Fact]
    public void Place_snapshots_product_data()
    {
        var keyboard = NewProduct(price: 100m, sku: "KB", name: "Keyboard");
        var order = Order.Place(CustomerId, [new(keyboard, 1)], Now).Value;

        keyboard.Update("Renamed keyboard", null, 999m, Now);

        var item = order.Items.ShouldHaveSingleItem();
        item.ProductName.ShouldBe("Keyboard");
        item.ProductSku.ShouldBe("KB");
        item.UnitPrice.ShouldBe(100m);
        item.LineTotal.ShouldBe(100m);
    }

    [Fact]
    public void Place_without_items_fails()
    {
        Order.Place(CustomerId, [], Now).Error.ShouldBe(OrderErrors.Empty);
    }

    [Fact]
    public void Place_with_the_same_product_twice_fails()
    {
        var keyboard = NewProduct();

        var result = Order.Place(CustomerId, [new(keyboard, 1), new(keyboard, 2)], Now);

        result.Error.ShouldBe(OrderErrors.DuplicateProduct);
        keyboard.StockQuantity.ShouldBe(10);
    }

    [Fact]
    public void Place_with_too_many_lines_fails()
    {
        var lines = Enumerable.Range(0, Order.MaxLines + 1)
            .Select(i => new OrderLine(NewProduct(sku: $"SKU-{i}"), 1))
            .ToList();

        Order.Place(CustomerId, lines, Now).Error.Code.ShouldBe("Orders.TooManyLines");
    }

    [Fact]
    public void Place_is_all_or_nothing_when_a_line_cannot_be_reserved()
    {
        var available = NewProduct(stock: 10, sku: "OK");
        var scarce = NewProduct(stock: 1, sku: "SCARCE");

        var result = Order.Place(CustomerId, [new(available, 5), new(scarce, 2)], Now);

        result.Error.Code.ShouldBe("Products.InsufficientStock");
        available.StockQuantity.ShouldBe(10);
        scarce.StockQuantity.ShouldBe(1);
    }

    [Fact]
    public void Order_follows_the_happy_path_lifecycle()
    {
        var order = PlaceOrder();

        order.MarkAsPaid(Now.AddMinutes(1)).IsSuccess.ShouldBeTrue();
        order.Ship(Now.AddMinutes(2)).IsSuccess.ShouldBeTrue();
        order.Deliver(Now.AddMinutes(3)).IsSuccess.ShouldBeTrue();

        order.Status.ShouldBe(OrderStatus.Delivered);
        order.PaidAt.ShouldBe(Now.AddMinutes(1));
        order.ShippedAt.ShouldBe(Now.AddMinutes(2));
        order.DeliveredAt.ShouldBe(Now.AddMinutes(3));
    }

    [Fact]
    public void An_unpaid_order_cannot_be_shipped()
    {
        var order = PlaceOrder();

        var result = order.Ship(Now);

        result.Error.Code.ShouldBe("Orders.InvalidStatusTransition");
        order.Status.ShouldBe(OrderStatus.Pending);
    }

    [Fact]
    public void An_order_cannot_be_paid_twice()
    {
        var order = PlaceOrder();
        order.MarkAsPaid(Now);

        order.MarkAsPaid(Now).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void An_order_must_be_shipped_before_it_is_delivered()
    {
        var order = PlaceOrder();
        order.MarkAsPaid(Now);

        order.Deliver(Now).IsFailure.ShouldBeTrue();
        order.Status.ShouldBe(OrderStatus.Paid);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancelling_a_pending_or_paid_order_returns_stock(bool paid)
    {
        var product = NewProduct(stock: 10);
        var order = Order.Place(CustomerId, [new(product, 4)], Now).Value;
        if (paid)
        {
            order.MarkAsPaid(Now);
        }

        var result = order.Cancel(Catalog(product), Now.AddHours(1));

        result.IsSuccess.ShouldBeTrue();
        order.Status.ShouldBe(OrderStatus.Cancelled);
        order.CancelledAt.ShouldBe(Now.AddHours(1));
        product.StockQuantity.ShouldBe(10);
    }

    [Fact]
    public void A_shipped_order_cannot_be_cancelled()
    {
        var product = NewProduct(stock: 10);
        var order = Order.Place(CustomerId, [new(product, 4)], Now).Value;
        order.MarkAsPaid(Now);
        order.Ship(Now);

        var result = order.Cancel(Catalog(product), Now);

        result.Error.Code.ShouldBe("Orders.InvalidStatusTransition");
        product.StockQuantity.ShouldBe(6);
    }

    [Fact]
    public void A_cancelled_order_cannot_be_cancelled_again()
    {
        var product = NewProduct(stock: 10);
        var order = Order.Place(CustomerId, [new(product, 4)], Now).Value;
        order.Cancel(Catalog(product), Now);

        order.Cancel(Catalog(product), Now).IsFailure.ShouldBeTrue();
        product.StockQuantity.ShouldBe(10);
    }

    [Fact]
    public void Cancel_requires_every_product_of_the_order()
    {
        var order = PlaceOrder();

        Should.Throw<ArgumentException>(() => order.Cancel(new Dictionary<Guid, Product>(), Now));
        order.Status.ShouldBe(OrderStatus.Pending);
    }

    [Fact]
    public void IsOwnedBy_matches_only_the_customer()
    {
        var order = PlaceOrder();

        order.IsOwnedBy(CustomerId).ShouldBeTrue();
        order.IsOwnedBy(Guid.CreateVersion7()).ShouldBeFalse();
    }

    private static Order PlaceOrder() => Order.Place(CustomerId, [new(NewProduct(), 1)], Now).Value;

    private static Dictionary<Guid, Product> Catalog(params Product[] products) =>
        products.ToDictionary(product => product.Id);
}
