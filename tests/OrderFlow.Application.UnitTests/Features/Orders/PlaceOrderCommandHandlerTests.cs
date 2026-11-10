using Microsoft.Extensions.Time.Testing;
using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Features.Orders;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Products;
using OrderFlow.Domain.Users;
using static OrderFlow.Application.UnitTests.TestData;

namespace OrderFlow.Application.UnitTests.Features.Orders;

public sealed class PlaceOrderCommandHandlerTests
{
    private static readonly Guid CustomerId = Guid.CreateVersion7();

    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly PlaceOrderCommandHandler _handler;

    public PlaceOrderCommandHandlerTests()
    {
        _currentUser.UserId.Returns(CustomerId);
        _handler = new PlaceOrderCommandHandler(_products, _orders, _currentUser, _unitOfWork, new FakeTimeProvider(Now));
    }

    [Fact]
    public async Task Places_the_order_reserves_stock_and_saves()
    {
        var product = NewProduct(price: 10m, stock: 5);
        GivenCatalog(product);

        var result = await _handler.Handle(new PlaceOrderCommand([new PlaceOrderItem(product.Id, 2)]), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(OrderStatus.Pending);
        result.Value.Total.ShouldBe(20m);
        result.Value.CustomerId.ShouldBe(CustomerId);
        product.StockQuantity.ShouldBe(3);
        _orders.Received(1).Add(Arg.Is<Order>(order => order.CustomerId == CustomerId));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_when_a_product_does_not_exist()
    {
        var existing = NewProduct();
        var missingId = Guid.CreateVersion7();
        GivenCatalog(existing);

        var result = await _handler.Handle(
            new PlaceOrderCommand([new PlaceOrderItem(existing.Id, 1), new PlaceOrderItem(missingId, 1)]), Ct);

        result.Error.ShouldBe(ProductErrors.NotFound(missingId));
        existing.StockQuantity.ShouldBe(10);
        await AssertNothingSaved();
    }

    [Fact]
    public async Task Fails_without_touching_stock_when_there_is_not_enough()
    {
        var product = NewProduct(stock: 1);
        GivenCatalog(product);

        var result = await _handler.Handle(new PlaceOrderCommand([new PlaceOrderItem(product.Id, 2)]), Ct);

        result.Error.Code.ShouldBe("Products.InsufficientStock");
        product.StockQuantity.ShouldBe(1);
        await AssertNothingSaved();
    }

    [Fact]
    public async Task Requires_an_authenticated_user()
    {
        _currentUser.UserId.Returns((Guid?)null);

        var result = await _handler.Handle(new PlaceOrderCommand([new PlaceOrderItem(Guid.CreateVersion7(), 1)]), Ct);

        result.Error.ShouldBe(UserErrors.NotAuthenticated);
        await AssertNothingSaved();
    }

    private void GivenCatalog(params Product[] products) =>
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Product>>(products));

    private async Task AssertNothingSaved()
    {
        _orders.DidNotReceive().Add(Arg.Any<Order>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
