using Microsoft.Extensions.Time.Testing;
using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Features.Orders;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Products;
using OrderFlow.Domain.Users;
using static OrderFlow.Application.UnitTests.TestData;

namespace OrderFlow.Application.UnitTests.Features.Orders;

public sealed class OrderLifecycleHandlerTests
{
    private static readonly Guid OwnerId = Guid.CreateVersion7();
    private static readonly Guid OtherCustomerId = Guid.CreateVersion7();

    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly Product _product = NewProduct(stock: 10);
    private readonly Order _order;

    public OrderLifecycleHandlerTests()
    {
        _order = Order.Place(OwnerId, [new OrderLine(_product, 4)], Now).Value;
        _orders.GetByIdAsync(_order.Id, Arg.Any<CancellationToken>()).Returns(_order);
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Product>>([_product]));
    }

    [Fact]
    public async Task Owner_can_cancel_and_stock_is_returned()
    {
        ActAs(OwnerId);

        var result = await CancelHandler().Handle(new CancelOrderCommand(_order.Id), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(OrderStatus.Cancelled);
        _product.StockQuantity.ShouldBe(10);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Another_customer_gets_not_found_instead_of_the_order()
    {
        ActAs(OtherCustomerId);

        var result = await CancelHandler().Handle(new CancelOrderCommand(_order.Id), Ct);

        result.Error.ShouldBe(OrderErrors.NotFound(_order.Id));
        _order.Status.ShouldBe(OrderStatus.Pending);
        _product.StockQuantity.ShouldBe(6);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Administrator_can_cancel_any_order()
    {
        ActAs(OtherCustomerId, isAdmin: true);

        var result = await CancelHandler().Handle(new CancelOrderCommand(_order.Id), Ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Owner_can_pay_a_pending_order()
    {
        ActAs(OwnerId);
        var handler = new PayOrderCommandHandler(_orders, _currentUser, _unitOfWork, _time);

        var result = await handler.Handle(new PayOrderCommand(_order.Id), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(OrderStatus.Paid);
        result.Value.PaidAt.ShouldBe(Now);
    }

    [Fact]
    public async Task Customers_cannot_ship_orders_even_their_own()
    {
        ActAs(OwnerId);
        _order.MarkAsPaid(Now);
        var handler = new ShipOrderCommandHandler(_orders, _currentUser, _unitOfWork, _time);

        var result = await handler.Handle(new ShipOrderCommand(_order.Id), Ct);

        result.Error.ShouldBe(UserErrors.Forbidden);
        _order.Status.ShouldBe(OrderStatus.Paid);
    }

    [Fact]
    public async Task Invalid_transitions_are_reported_and_not_saved()
    {
        ActAs(OwnerId, isAdmin: true);
        var handler = new DeliverOrderCommandHandler(_orders, _currentUser, _unitOfWork, _time);

        var result = await handler.Handle(new DeliverOrderCommand(_order.Id), Ct);

        result.Error.Code.ShouldBe("Orders.InvalidStatusTransition");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private CancelOrderCommandHandler CancelHandler() =>
        new(_orders, _products, _currentUser, _unitOfWork, _time);

    private void ActAs(Guid userId, bool isAdmin = false)
    {
        _currentUser.UserId.Returns(userId);
        _currentUser.IsAdmin.Returns(isAdmin);
    }
}
