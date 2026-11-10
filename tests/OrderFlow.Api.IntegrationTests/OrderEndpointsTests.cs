using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Api.IntegrationTests.Infrastructure;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Common;
using OrderFlow.Application.Features.Orders;
using OrderFlow.Application.Features.Products;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class OrderEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Placing_an_order_reserves_stock()
    {
        var admin = await CreateAdminAsync();
        var product = await CreateProductAsync(admin, price: 20m, stock: 10);
        var (customer, auth) = await CreateCustomerAsync();

        var response = await PlaceAsync(customer, (product.Id, 3));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        var order = await ReadAsync<OrderResponse>(response);
        order.Status.ShouldBe(OrderStatus.Pending);
        order.CustomerId.ShouldBe(auth.User.Id);
        order.Total.ShouldBe(60m);
        order.Items.ShouldHaveSingleItem().Quantity.ShouldBe(3);
        (await GetProductAsync(product.Id)).StockQuantity.ShouldBe(7);
    }

    [Fact]
    public async Task Ordering_more_than_available_returns_409_and_keeps_stock()
    {
        var admin = await CreateAdminAsync();
        var plenty = await CreateProductAsync(admin, stock: 10);
        var scarce = await CreateProductAsync(admin, stock: 1);
        var (customer, _) = await CreateCustomerAsync();

        var response = await PlaceAsync(customer, (plenty.Id, 2), (scarce.Id, 2));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadProblemAsync(response)).GetProperty("code").GetString().ShouldBe("Products.InsufficientStock");
        (await GetProductAsync(plenty.Id)).StockQuantity.ShouldBe(10);
        (await GetProductAsync(scarce.Id)).StockQuantity.ShouldBe(1);
    }

    [Fact]
    public async Task Ordering_an_unknown_product_returns_404()
    {
        var (customer, _) = await CreateCustomerAsync();

        var response = await PlaceAsync(customer, (Guid.CreateVersion7(), 1));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invalid_order_returns_validation_errors_with_json_paths()
    {
        var (customer, _) = await CreateCustomerAsync();

        var response = await PlaceAsync(customer, (Guid.Empty, 0));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await ReadProblemAsync(response)).GetProperty("errors");
        errors.TryGetProperty("items[0].productId", out _).ShouldBeTrue();
        errors.TryGetProperty("items[0].quantity", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Anonymous_users_cannot_place_orders()
    {
        var response = await PlaceAsync(CreateClient(), (Guid.CreateVersion7(), 1));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Customers_only_see_their_own_orders()
    {
        var admin = await CreateAdminAsync();
        var product = await CreateProductAsync(admin);
        var (alice, _) = await CreateCustomerAsync();
        var (bob, _) = await CreateCustomerAsync();
        var aliceOrder = await ReadAsync<OrderResponse>(await PlaceAsync(alice, (product.Id, 1)));

        var bobView = await bob.GetAsync($"/api/v1/orders/{aliceOrder.Id}", Ct);
        bobView.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var bobList = await ReadAsync<PagedList<OrderResponse>>(await bob.GetAsync("/api/v1/orders", Ct));
        bobList.Items.ShouldNotContain(order => order.Id == aliceOrder.Id);

        var aliceList = await ReadAsync<PagedList<OrderResponse>>(await alice.GetAsync("/api/v1/orders", Ct));
        aliceList.Items.ShouldHaveSingleItem().Id.ShouldBe(aliceOrder.Id);

        var adminView = await admin.GetAsync($"/api/v1/orders/{aliceOrder.Id}", Ct);
        adminView.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Order_goes_through_the_whole_lifecycle()
    {
        var admin = await CreateAdminAsync();
        var product = await CreateProductAsync(admin);
        var (customer, _) = await CreateCustomerAsync();
        var order = await ReadAsync<OrderResponse>(await PlaceAsync(customer, (product.Id, 1)));

        (await customer.PostAsync($"/api/v1/orders/{order.Id}/pay", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await customer.PostAsync($"/api/v1/orders/{order.Id}/ship", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.PostAsync($"/api/v1/orders/{order.Id}/ship", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var delivered = await admin.PostAsync($"/api/v1/orders/{order.Id}/deliver", null, Ct);

        delivered.StatusCode.ShouldBe(HttpStatusCode.OK);
        var final = await ReadAsync<OrderResponse>(delivered);
        final.Status.ShouldBe(OrderStatus.Delivered);
        final.PaidAt.ShouldNotBeNull();
        final.ShippedAt.ShouldNotBeNull();
        final.DeliveredAt.ShouldNotBeNull();

        var filtered = await ReadAsync<PagedList<OrderResponse>>(
            await customer.GetAsync("/api/v1/orders?status=delivered", Ct));
        filtered.Items.ShouldContain(o => o.Id == order.Id);
    }

    [Fact]
    public async Task Cancelling_returns_stock_and_cannot_be_repeated()
    {
        var admin = await CreateAdminAsync();
        var product = await CreateProductAsync(admin, stock: 5);
        var (customer, _) = await CreateCustomerAsync();
        var order = await ReadAsync<OrderResponse>(await PlaceAsync(customer, (product.Id, 5)));
        (await GetProductAsync(product.Id)).StockQuantity.ShouldBe(0);

        var cancel = await customer.PostAsync($"/api/v1/orders/{order.Id}/cancel", null, Ct);

        cancel.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<OrderResponse>(cancel)).Status.ShouldBe(OrderStatus.Cancelled);
        (await GetProductAsync(product.Id)).StockQuantity.ShouldBe(5);

        var again = await customer.PostAsync($"/api/v1/orders/{order.Id}/cancel", null, Ct);
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Concurrent_orders_never_oversell_the_last_unit()
    {
        const int Buyers = 5;
        var admin = await CreateAdminAsync();
        var product = await CreateProductAsync(admin, stock: 1);
        var customers = await Task.WhenAll(Enumerable.Range(0, Buyers).Select(_ => CreateCustomerAsync()));

        var responses = await Task.WhenAll(customers.Select(c => PlaceAsync(c.Client, (product.Id, 1))));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(Buyers - 1);
        foreach (var conflict in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
        {
            // Losers are retried with fresh data, so they see the real reason: no stock left.
            (await ReadProblemAsync(conflict)).GetProperty("code").GetString()
                .ShouldBeOneOf("Products.InsufficientStock", "General.ConcurrencyConflict");
        }

        (await GetProductAsync(product.Id)).StockQuantity.ShouldBe(0);
    }

    [Fact]
    public async Task Concurrent_orders_for_a_well_stocked_product_all_succeed()
    {
        const int Buyers = 3;
        var admin = await CreateAdminAsync();
        var product = await CreateProductAsync(admin, stock: Buyers);
        var customers = await Task.WhenAll(Enumerable.Range(0, Buyers).Select(_ => CreateCustomerAsync()));

        var responses = await Task.WhenAll(customers.Select(c => PlaceAsync(c.Client, (product.Id, 1))));

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Created);
        (await GetProductAsync(product.Id)).StockQuantity.ShouldBe(0);
    }

    [Fact]
    public async Task Paying_and_cancelling_the_same_order_concurrently_cannot_both_succeed()
    {
        var admin = await CreateAdminAsync();
        var product = await CreateProductAsync(admin, stock: 5);
        var (customer, _) = await CreateCustomerAsync();
        var placed = await ReadAsync<OrderResponse>(await PlaceAsync(customer, (product.Id, 2)));

        // Two requests read the same version of the order...
        await using var payScope = Factory.Services.CreateAsyncScope();
        await using var cancelScope = Factory.Services.CreateAsyncScope();
        var toPay = (await payScope.ServiceProvider.GetRequiredService<IOrderRepository>().GetByIdAsync(placed.Id, Ct))!;
        var toCancel = (await cancelScope.ServiceProvider.GetRequiredService<IOrderRepository>().GetByIdAsync(placed.Id, Ct))!;
        var catalog = (await cancelScope.ServiceProvider.GetRequiredService<IProductRepository>()
            .GetByIdsAsync([product.Id], Ct)).ToDictionary(p => p.Id);

        // ...the payment is saved first...
        toPay.MarkAsPaid(DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
        await payScope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);

        // ...so the cancellation, based on stale data, must be rejected instead of overwriting it.
        toCancel.Cancel(catalog, DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
        await Should.ThrowAsync<ConcurrencyConflictException>(
            () => cancelScope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct));

        var order = await ReadAsync<OrderResponse>(await customer.GetAsync($"/api/v1/orders/{placed.Id}", Ct));
        order.Status.ShouldBe(OrderStatus.Paid);
        (await GetProductAsync(product.Id)).StockQuantity.ShouldBe(3);
    }

    [Theory]
    [InlineData("Pending,Paid")]
    [InlineData("3")]
    [InlineData("Unknown")]
    public async Task Invalid_status_filters_are_rejected(string status)
    {
        var (customer, _) = await CreateCustomerAsync();

        var response = await customer.GetAsync($"/api/v1/orders?status={Uri.EscapeDataString(status)}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadProblemAsync(response)).GetProperty("errors").TryGetProperty("status", out _).ShouldBeTrue();
    }

    private static Task<HttpResponseMessage> PlaceAsync(HttpClient client, params (Guid ProductId, int Quantity)[] items) =>
        client.PostAsJsonAsync(
            "/api/v1/orders",
            new PlaceOrderCommand(items.Select(item => new PlaceOrderItem(item.ProductId, item.Quantity)).ToList()),
            Ct);

    private async Task<ProductResponse> GetProductAsync(Guid id) =>
        await ReadAsync<ProductResponse>(await CreateClient().GetAsync($"/api/v1/products/{id}", Ct));
}
