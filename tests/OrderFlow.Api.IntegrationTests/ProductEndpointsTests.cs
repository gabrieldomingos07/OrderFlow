using OrderFlow.Api.IntegrationTests.Infrastructure;
using OrderFlow.Application.Common;
using OrderFlow.Application.Features.Products;

namespace OrderFlow.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ProductEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Anonymous_users_cannot_create_products()
    {
        var response = await CreateClient().PostAsJsonAsync("/api/v1/products", ValidProduct(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Customers_cannot_create_products()
    {
        var (customer, _) = await CreateCustomerAsync();

        var response = await customer.PostAsJsonAsync("/api/v1/products", ValidProduct(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_creates_a_product_that_anyone_can_read()
    {
        var admin = await CreateAdminAsync();
        var command = ValidProduct();

        var response = await admin.PostAsJsonAsync("/api/v1/products", command, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        var created = await ReadAsync<ProductResponse>(response);
        created.Sku.ShouldBe(command.Sku.ToUpperInvariant());

        var fetched = await CreateClient().GetAsync(response.Headers.Location, Ct);
        fetched.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<ProductResponse>(fetched)).Id.ShouldBe(created.Id);
    }

    [Fact]
    public async Task Duplicate_sku_returns_409()
    {
        var admin = await CreateAdminAsync();
        var existing = await CreateProductAsync(admin);

        var response = await admin.PostAsJsonAsync(
            "/api/v1/products",
            ValidProduct() with { Sku = existing.Sku.ToLowerInvariant() },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadProblemAsync(response)).GetProperty("code").GetString().ShouldBe("Products.SkuAlreadyExists");
    }

    [Fact]
    public async Task Invalid_product_returns_validation_problem()
    {
        var admin = await CreateAdminAsync();

        var response = await admin.PostAsJsonAsync(
            "/api/v1/products",
            new CreateProductCommand("bad sku!", "", null, -1m, -5),
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await ReadProblemAsync(response)).GetProperty("errors");
        foreach (var field in new[] { "sku", "name", "price", "initialStock" })
        {
            errors.TryGetProperty(field, out _).ShouldBeTrue($"Expected a validation error for '{field}'.");
        }
    }

    [Fact]
    public async Task Admin_updates_and_restocks_a_product()
    {
        var admin = await CreateAdminAsync();
        var product = await CreateProductAsync(admin, price: 10m, stock: 1);

        var update = await admin.PutAsJsonAsync(
            $"/api/v1/products/{product.Id}",
            new { name = "Renamed", description = "Now with RGB", price = 12.5m },
            Ct);
        update.StatusCode.ShouldBe(HttpStatusCode.OK);

        var restock = await admin.PostAsJsonAsync($"/api/v1/products/{product.Id}/restock", new { quantity = 9 }, Ct);
        restock.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await ReadAsync<ProductResponse>(restock);
        updated.Name.ShouldBe("Renamed");
        updated.Price.ShouldBe(12.5m);
        updated.StockQuantity.ShouldBe(10);
        updated.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Deactivated_products_are_hidden_from_the_public()
    {
        var admin = await CreateAdminAsync();
        var product = await CreateProductAsync(admin);

        var delete = await admin.DeleteAsync($"/api/v1/products/{product.Id}", Ct);
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var publicView = await CreateClient().GetAsync($"/api/v1/products/{product.Id}", Ct);
        publicView.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var adminView = await admin.GetAsync($"/api/v1/products/{product.Id}", Ct);
        adminView.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<ProductResponse>(adminView)).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task List_searches_by_name_or_sku_and_paginates()
    {
        var admin = await CreateAdminAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 3; i++)
        {
            await admin.PostAsJsonAsync(
                "/api/v1/products",
                new CreateProductCommand($"S-{marker}-{i}", $"Searchable {marker} {i}", null, 10m, 1),
                Ct);
        }

        var response = await CreateClient().GetAsync($"/api/v1/products?search={marker}&page=1&pageSize=2", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadAsync<PagedList<ProductResponse>>(response);
        page.TotalCount.ShouldBe(3);
        page.Items.Count.ShouldBe(2);
        page.HasNextPage.ShouldBeTrue();
    }

    [Theory]
    [InlineData("%", "X")]
    [InlineData("_", "X")]
    public async Task Like_wildcards_in_the_search_are_matched_literally(string wildcard, string lookalike)
    {
        var admin = await CreateAdminAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var literal = await CreateNamedProductAsync(admin, $"{marker} 50{wildcard} off");
        await CreateNamedProductAsync(admin, $"{marker} 50{lookalike} off");

        var response = await CreateClient().GetAsync(
            $"/api/v1/products?search={Uri.EscapeDataString($"{marker} 50{wildcard}")}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadAsync<PagedList<ProductResponse>>(response);
        page.Items.ShouldHaveSingleItem().Id.ShouldBe(literal.Id);
    }

    [Fact]
    public async Task Search_also_matches_the_sku()
    {
        var admin = await CreateAdminAsync();
        var product = await CreateNamedProductAsync(admin, "A name without the code");

        var response = await CreateClient().GetAsync($"/api/v1/products?search={product.Sku[2..12].ToLowerInvariant()}", Ct);

        (await ReadAsync<PagedList<ProductResponse>>(response)).Items.ShouldContain(p => p.Id == product.Id);
    }

    [Theory]
    [InlineData("pageSize=1000")]
    [InlineData("page=0")]
    [InlineData("page=2147483647")]
    [InlineData("search=a%00b")]
    public async Task Invalid_list_parameters_return_validation_problem(string query)
    {
        var response = await CreateClient().GetAsync($"/api/v1/products?{query}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadProblemAsync(response)).GetProperty("code").GetString().ShouldBe("Validation.Failed");
    }

    private async Task<ProductResponse> CreateNamedProductAsync(HttpClient admin, string name)
    {
        var response = await admin.PostAsJsonAsync(
            "/api/v1/products",
            new CreateProductCommand($"N-{Guid.NewGuid():N}"[..16], name, null, 10m, 1),
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await ReadAsync<ProductResponse>(response);
    }

    private static CreateProductCommand ValidProduct() =>
        new($"p-{Guid.NewGuid():N}"[..16], "Mechanical Keyboard", "ABNT2", 299.90m, 10);
}
