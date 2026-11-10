using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OrderFlow.Api.IntegrationTests.Infrastructure;
using OrderFlow.Application.Features.Auth;

namespace OrderFlow.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class SecurityTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Auth_endpoints_are_rate_limited_per_client()
    {
        await using var limited = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["RateLimiting:Auth:PermitLimit"] = "2" })));
        var client = limited.CreateClient();
        var wrongCredentials = new LoginCommand("nobody@example.com", "WrongPassword1");

        (await client.PostAsJsonAsync("/api/v1/auth/login", wrongCredentials, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/v1/auth/login", wrongCredentials, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var limitedResponse = await client.PostAsJsonAsync("/api/v1/auth/login", wrongCredentials, Ct);

        limitedResponse.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        limitedResponse.Headers.RetryAfter.ShouldNotBeNull();
        (await ReadProblemAsync(limitedResponse)).GetProperty("code").GetString().ShouldBe("General.TooManyRequests");

        // Registration shares the same budget.
        var register = await client.PostAsJsonAsync(
            "/api/v1/auth/register", new RegisterCommand("someone@example.com", "Someone", "Secret123"), Ct);
        register.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task A_token_minted_like_the_api_does_is_accepted()
    {
        var (_, auth) = await CreateCustomerAsync();

        var response = await CreateClient(MintToken(auth.User.Id)).GetAsync("/api/v1/auth/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public static TheoryData<string> InvalidTokens() =>
    [
        "expired",
        "wrong-issuer",
        "wrong-audience",
        "wrong-key",
        "wrong-algorithm",
    ];

    [Theory]
    [MemberData(nameof(InvalidTokens))]
    public async Task Invalid_tokens_are_rejected(string kind)
    {
        var (_, auth) = await CreateCustomerAsync();
        var token = kind switch
        {
            "expired" => MintToken(auth.User.Id, expires: DateTime.UtcNow.AddMinutes(-5)),
            "wrong-issuer" => MintToken(auth.User.Id, issuer: "someone-else"),
            "wrong-audience" => MintToken(auth.User.Id, audience: "another-api"),
            "wrong-key" => MintToken(auth.User.Id, key: "a-completely-different-signing-key-0123456789"),
            "wrong-algorithm" => MintToken(auth.User.Id, algorithm: SecurityAlgorithms.HmacSha512),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        var response = await CreateClient(token).GetAsync("/api/v1/auth/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Framework_errors_follow_the_same_problem_contract()
    {
        var response = await CreateClient().GetAsync("/api/v1/orders", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var problem = await ReadProblemAsync(response);
        problem.GetProperty("code").GetString().ShouldBe("General.Unauthorized");
        problem.GetProperty("instance").GetString().ShouldBe("/api/v1/orders");
        problem.TryGetProperty("traceId", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Malformed_json_returns_a_problem_without_internal_details()
    {
        using var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        var response = await CreateClient().PostAsync("/api/v1/auth/login", content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ReadProblemAsync(response);
        problem.GetProperty("code").GetString().ShouldBe("General.BadRequest");
        problem.ToString().ShouldNotContain("LoginCommand");
    }

    private static string MintToken(
        Guid userId,
        DateTime? expires = null,
        string issuer = ApiFactory.JwtIssuer,
        string audience = ApiFactory.JwtAudience,
        string key = ApiFactory.JwtSigningKey,
        string algorithm = SecurityAlgorithms.HmacSha256)
    {
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(10);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = expiresAt.AddMinutes(-20),
            NotBefore = expiresAt.AddMinutes(-20),
            Expires = expiresAt,
            Subject = new ClaimsIdentity([new Claim("sub", userId.ToString()), new Claim("role", "Customer")]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), algorithm),
        });
    }
}
