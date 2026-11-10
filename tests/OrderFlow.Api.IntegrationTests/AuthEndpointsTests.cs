using OrderFlow.Api.IntegrationTests.Infrastructure;
using OrderFlow.Application.Features.Auth;
using OrderFlow.Domain.Users;

namespace OrderFlow.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AuthEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Register_creates_a_customer_and_returns_a_token()
    {
        var (_, auth) = await CreateCustomerAsync();

        auth.AccessToken.ShouldNotBeNullOrWhiteSpace();
        auth.TokenType.ShouldBe("Bearer");
        auth.User.Role.ShouldBe(UserRole.Customer);
    }

    [Fact]
    public async Task Register_with_an_existing_email_returns_409()
    {
        var (_, auth) = await CreateCustomerAsync();

        var response = await CreateClient().PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand(auth.User.Email.ToUpperInvariant(), "Someone Else", "Secret123"),
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await ReadProblemAsync(response);
        problem.GetProperty("code").GetString().ShouldBe("Users.EmailAlreadyInUse");
    }

    [Fact]
    public async Task Register_with_invalid_data_returns_validation_problem()
    {
        var response = await CreateClient().PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterCommand("not-an-email", "", "weak"),
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await ReadProblemAsync(response)).GetProperty("errors");
        errors.TryGetProperty("email", out _).ShouldBeTrue();
        errors.TryGetProperty("fullName", out _).ShouldBeTrue();
        errors.TryGetProperty("password", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Login_returns_a_token_that_identifies_the_user()
    {
        var (_, registered) = await CreateCustomerAsync();

        var login = await CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginCommand(registered.User.Email, "Secret123"),
            Ct);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth = await ReadAsync<AuthResponse>(login);

        var me = await CreateClient(auth.AccessToken).GetAsync("/api/v1/auth/me", Ct);

        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<UserResponse>(me)).Id.ShouldBe(registered.User.Id);
    }

    [Fact]
    public async Task Login_with_a_wrong_password_returns_401()
    {
        var (_, registered) = await CreateCustomerAsync();

        var response = await CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginCommand(registered.User.Email, "WrongPassword1"),
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ReadProblemAsync(response)).GetProperty("code").GetString().ShouldBe("Users.InvalidCredentials");
    }

    [Fact]
    public async Task Me_without_a_token_returns_401()
    {
        var response = await CreateClient().GetAsync("/api/v1/auth/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_tampered_token_is_rejected()
    {
        var (_, auth) = await CreateCustomerAsync();
        var tampered = auth.AccessToken[..^4] + "abcd";

        var response = await CreateClient(tampered).GetAsync("/api/v1/auth/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
