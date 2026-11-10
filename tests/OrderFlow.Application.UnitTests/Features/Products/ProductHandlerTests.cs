using Microsoft.Extensions.Time.Testing;
using NSubstitute.ExceptionExtensions;
using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Common;
using OrderFlow.Application.Features.Products;
using OrderFlow.Domain.Products;
using static OrderFlow.Application.UnitTests.TestData;

namespace OrderFlow.Application.UnitTests.Features.Products;

public sealed class ProductHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _time = new(Now);

    [Fact]
    public async Task Create_rejects_a_duplicate_sku_regardless_of_case()
    {
        _products.SkuExistsAsync("KB-01", Arg.Any<CancellationToken>()).Returns(true);
        var handler = new CreateProductCommandHandler(_products, _unitOfWork, _time);

        var result = await handler.Handle(new CreateProductCommand("kb-01", "Keyboard", null, 10m, 1), Ct);

        result.Error.ShouldBe(ProductErrors.SkuAlreadyExists("KB-01"));
        _products.DidNotReceive().Add(Arg.Any<Product>());
    }

    [Fact]
    public async Task Create_reports_a_concurrent_duplicate_sku_as_a_conflict()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new UniqueConstraintViolationException("ix_products_sku"));
        var handler = new CreateProductCommandHandler(_products, _unitOfWork, _time);

        var result = await handler.Handle(new CreateProductCommand("kb-01", "Keyboard", null, 10m, 1), Ct);

        result.Error.ShouldBe(ProductErrors.SkuAlreadyExists("KB-01"));
    }

    [Fact]
    public async Task Create_adds_the_product_and_saves()
    {
        var handler = new CreateProductCommandHandler(_products, _unitOfWork, _time);

        var result = await handler.Handle(new CreateProductCommand("kb-01", "Keyboard", "Mechanical", 10m, 3), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Sku.ShouldBe("KB-01");
        result.Value.StockQuantity.ShouldBe(3);
        result.Value.CreatedAt.ShouldBe(Now);
        _products.Received(1).Add(Arg.Is<Product>(product => product.Sku == "KB-01"));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Inactive_products_are_visible_to_administrators_only(bool isAdmin, bool visible)
    {
        var product = NewProduct();
        product.Deactivate(Now);
        _products.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        _currentUser.IsAdmin.Returns(isAdmin);
        var handler = new GetProductByIdQueryHandler(_products, _currentUser);

        var result = await handler.Handle(new GetProductByIdQuery(product.Id), Ct);

        result.IsSuccess.ShouldBe(visible);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task List_honours_include_inactive_only_for_administrators(bool isAdmin, bool expectedIncludeInactive)
    {
        _currentUser.IsAdmin.Returns(isAdmin);
        _products.SearchAsync(Arg.Any<ProductSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new PagedList<Product>([], 1, 20, 0));
        var handler = new ListProductsQueryHandler(_products, _currentUser);

        await handler.Handle(new ListProductsQuery("kb", IncludeInactive: true), Ct);

        await _products.Received(1).SearchAsync(
            Arg.Is<ProductSearchCriteria>(criteria => criteria.IncludeInactive == expectedIncludeInactive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Restock_of_an_unknown_product_returns_not_found()
    {
        var id = Guid.CreateVersion7();
        var handler = new RestockProductCommandHandler(_products, _unitOfWork, _time);

        var result = await handler.Handle(new RestockProductCommand(id, 5), Ct);

        result.Error.ShouldBe(ProductErrors.NotFound(id));
    }
}
