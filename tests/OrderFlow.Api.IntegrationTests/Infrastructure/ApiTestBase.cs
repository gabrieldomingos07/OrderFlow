using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrderFlow.Application.Features.Auth;
using OrderFlow.Application.Features.Products;

namespace OrderFlow.Api.IntegrationTests.Infrastructure;

public abstract class ApiTestBase(ApiFactory factory)
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected ApiFactory Factory { get; } = factory;

    protected HttpClient CreateClient(string? accessToken = null)
    {
        var client = Factory.CreateClient();
        if (accessToken is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client;
    }

    /// <summary>Registers a brand-new customer (unique e-mail) and returns a client signed in as them.</summary>
    protected async Task<(HttpClient Client, AuthResponse Auth)> CreateCustomerAsync()
    {
        var email = $"customer-{Guid.NewGuid():N}@example.com";
        var response = await CreateClient().PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(email, "Test Customer", "Secret123"),
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var auth = await ReadAsync<AuthResponse>(response);
        return (CreateClient(auth.AccessToken), auth);
    }

    protected async Task<HttpClient> CreateAdminAsync()
    {
        var response = await CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginCommand(ApiFactory.AdminEmail, ApiFactory.AdminPassword),
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var auth = await ReadAsync<AuthResponse>(response);
        return CreateClient(auth.AccessToken);
    }

    protected async Task<ProductResponse> CreateProductAsync(HttpClient admin, decimal price = 50m, int stock = 10)
    {
        var sku = $"T-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var response = await admin.PostAsJsonAsync(
            "/api/v1/products",
            new CreateProductCommand(sku, $"Test product {sku}", null, price, stock),
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ReadAsync<ProductResponse>(response);
    }

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json, Ct);
        if (value is null)
        {
            throw new InvalidOperationException($"The response body could not be read as {typeof(T).Name}.");
        }

        return value;
    }

    protected static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        return await ReadAsync<JsonElement>(response);
    }
}
