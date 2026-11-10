using Microsoft.Extensions.Time.Testing;
using NSubstitute.ExceptionExtensions;
using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Common;
using OrderFlow.Application.Features.Auth;
using OrderFlow.Domain.Users;
using static OrderFlow.Application.UnitTests.TestData;

namespace OrderFlow.Application.UnitTests.Features.Auth;

public sealed class AuthHandlerTests
{
    private static readonly AccessToken Token = new("jwt-token", Now.AddHours(1));

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenProvider _tokenProvider = Substitute.For<ITokenProvider>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public AuthHandlerTests()
    {
        _passwordHasher.Hash(Arg.Any<string>()).Returns(call => "hashed:" + call.Arg<string>());
        _tokenProvider.Create(Arg.Any<User>()).Returns(Token);
    }

    [Fact]
    public async Task Register_creates_a_customer_with_a_hashed_password()
    {
        var handler = RegisterHandler();

        var result = await handler.Handle(new RegisterCommand("  Jane@Example.COM ", "Jane Doe", "Secret123"), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe(Token.Token);
        result.Value.TokenType.ShouldBe("Bearer");
        result.Value.User.Email.ShouldBe("jane@example.com");
        result.Value.User.Role.ShouldBe(UserRole.Customer);
        _users.Received(1).Add(Arg.Is<User>(user =>
            user.Email == "jane@example.com" && user.PasswordHash == "hashed:Secret123"));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_rejects_an_email_already_in_use()
    {
        _users.EmailExistsAsync("jane@example.com", Arg.Any<CancellationToken>()).Returns(true);

        var result = await RegisterHandler().Handle(new RegisterCommand("jane@example.com", "Jane", "Secret123"), Ct);

        result.Error.ShouldBe(UserErrors.EmailAlreadyInUse);
        _users.DidNotReceive().Add(Arg.Any<User>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_reports_a_concurrent_duplicate_as_email_already_in_use()
    {
        // Both requests passed the existence check; the unique index rejects the second insert.
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new UniqueConstraintViolationException("ix_users_email"));

        var result = await RegisterHandler().Handle(new RegisterCommand("jane@example.com", "Jane", "Secret123"), Ct);

        result.Error.ShouldBe(UserErrors.EmailAlreadyInUse);
        _tokenProvider.DidNotReceive().Create(Arg.Any<User>());
    }

    [Fact]
    public async Task Login_returns_a_token_for_valid_credentials()
    {
        var user = NewUser();
        _users.GetByEmailAsync("jane@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("Secret123", user.PasswordHash).Returns(true);

        var result = await LoginHandler().Handle(new LoginCommand("JANE@example.com", "Secret123"), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe(Token.Token);
        result.Value.User.Id.ShouldBe(user.Id);
    }

    [Fact]
    public async Task Login_with_a_wrong_password_fails()
    {
        _users.GetByEmailAsync("jane@example.com", Arg.Any<CancellationToken>()).Returns(NewUser());
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var result = await LoginHandler().Handle(new LoginCommand("jane@example.com", "wrong"), Ct);

        result.Error.ShouldBe(UserErrors.InvalidCredentials);
        _tokenProvider.DidNotReceive().Create(Arg.Any<User>());
    }

    [Fact]
    public async Task Login_with_an_unknown_email_fails_the_same_way_and_still_hashes()
    {
        var result = await LoginHandler().Handle(new LoginCommand("nobody@example.com", "Secret123"), Ct);

        result.Error.ShouldBe(UserErrors.InvalidCredentials);
        _passwordHasher.Received(1).Hash("Secret123");
    }

    private RegisterCommandHandler RegisterHandler() =>
        new(_users, _passwordHasher, _tokenProvider, _unitOfWork, new FakeTimeProvider(Now));

    private LoginCommandHandler LoginHandler() => new(_users, _passwordHasher, _tokenProvider);
}
