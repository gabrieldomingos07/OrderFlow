using FluentValidation;
using OrderFlow.Application.Common;
using OrderFlow.Application.Features.Auth;
using OrderFlow.Application.Features.Orders;
using OrderFlow.Application.Features.Products;
using OrderFlow.Domain.Products;
using static OrderFlow.Application.UnitTests.TestData;

namespace OrderFlow.Application.UnitTests.Validation;

public sealed class ValidatorTests
{
    [Theory]
    [InlineData("short1A")]
    [InlineData("alllowercase1")]
    [InlineData("ALLUPPERCASE1")]
    [InlineData("NoDigitsHere")]
    [InlineData("")]
    public async Task Register_rejects_weak_passwords(string password)
    {
        var result = await new RegisterCommandValidator()
            .ValidateAsync(new RegisterCommand("jane@example.com", "Jane", password), Ct);

        result.Errors.ShouldContain(error => error.PropertyName == nameof(RegisterCommand.Password));
    }

    [Fact]
    public async Task Register_accepts_a_valid_command()
    {
        var result = await new RegisterCommandValidator()
            .ValidateAsync(new RegisterCommand("jane@example.com", "Jane Doe", "Secret123"), Ct);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Register_rejects_an_invalid_email()
    {
        var result = await new RegisterCommandValidator()
            .ValidateAsync(new RegisterCommand("not-an-email", "Jane", "Secret123"), Ct);

        result.Errors.ShouldContain(error => error.PropertyName == nameof(RegisterCommand.Email));
    }

    [Theory]
    [InlineData("KB 01", 10)]
    [InlineData("KB-01", 10.999)]
    [InlineData("KB-01", 0)]
    public async Task CreateProduct_rejects_invalid_sku_or_price(string sku, decimal price)
    {
        var result = await new CreateProductCommandValidator()
            .ValidateAsync(new CreateProductCommand(sku, "Keyboard", null, price, 1), Ct);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task PlaceOrder_requires_at_least_one_item()
    {
        var result = await new PlaceOrderCommandValidator().ValidateAsync(new PlaceOrderCommand([]), Ct);

        result.Errors.ShouldContain(error => error.PropertyName == nameof(PlaceOrderCommand.Items));
    }

    [Fact]
    public async Task PlaceOrder_handles_a_missing_item_list()
    {
        var result = await new PlaceOrderCommandValidator().ValidateAsync(new PlaceOrderCommand(null!), Ct);

        result.Errors.ShouldContain(error => error.PropertyName == nameof(PlaceOrderCommand.Items));
    }

    [Fact]
    public async Task PlaceOrder_rejects_duplicate_products()
    {
        var productId = Guid.CreateVersion7();

        var result = await new PlaceOrderCommandValidator().ValidateAsync(
            new PlaceOrderCommand([new PlaceOrderItem(productId, 1), new PlaceOrderItem(productId, 2)]), Ct);

        result.Errors.ShouldContain(error => error.ErrorMessage.Contains("only once", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(PlaceOrderCommandValidator.MaxQuantityPerItem + 1)]
    public async Task PlaceOrder_rejects_out_of_range_quantities(int quantity)
    {
        var result = await new PlaceOrderCommandValidator().ValidateAsync(
            new PlaceOrderCommand([new PlaceOrderItem(Guid.CreateVersion7(), quantity)]), Ct);

        result.Errors.ShouldContain(error => error.PropertyName == "Items[0].Quantity");
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 20)]
    [InlineData(Pagination.MaxPage + 1, 100)]
    public async Task List_queries_reject_invalid_pagination(int page, int pageSize)
    {
        IValidator<ListProductsQuery> products = new ListProductsQueryValidator();
        IValidator<ListOrdersQuery> orders = new ListOrdersQueryValidator();

        (await products.ValidateAsync(new ListProductsQuery(null, page, pageSize), Ct)).IsValid.ShouldBeFalse();
        (await orders.ValidateAsync(new ListOrdersQuery(null, page, pageSize), Ct)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task The_last_allowed_page_is_accepted()
    {
        var result = await new ListProductsQueryValidator()
            .ValidateAsync(new ListProductsQuery(null, Pagination.MaxPage, Pagination.MaxPageSize), Ct);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Null_characters_are_rejected_before_reaching_the_database()
    {
        (await new ListProductsQueryValidator().ValidateAsync(new ListProductsQuery("a\0b"), Ct)).IsValid.ShouldBeFalse();
        (await new LoginCommandValidator().ValidateAsync(new LoginCommand("a\0@x.com", "Secret123"), Ct)).IsValid.ShouldBeFalse();
        (await new RegisterCommandValidator().ValidateAsync(new RegisterCommand("jane@example.com", "Ja\0ne", "Secret123"), Ct))
            .IsValid.ShouldBeFalse();
        (await new CreateProductCommandValidator().ValidateAsync(new CreateProductCommand("KB-01", "Key\0board", null, 10m, 1), Ct))
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateProduct_rejects_a_price_above_the_maximum()
    {
        var result = await new CreateProductCommandValidator()
            .ValidateAsync(new CreateProductCommand("KB-01", "Keyboard", null, Product.MaxPrice + 0.01m, 1), Ct);

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CreateProductCommand.Price));
    }
}
