using FluentValidation;
using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Common;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Users;

namespace OrderFlow.Application.Features.Auth;

/// <summary>Exchanges e-mail and password for a JWT access token.</summary>
public sealed record LoginCommand(string Email, string Password) : ICommand<AuthResponse>;

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(User.EmailMaxLength).NoNullCharacters();
        RuleFor(command => command.Password).NotEmpty().MaximumLength(PasswordRules.MaxLength);
    }
}

internal sealed class LoginCommandHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider)
    : ICommandHandler<LoginCommand, AuthResponse>
{
    public async Task<Result<AuthResponse>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByEmailAsync(User.NormalizeEmail(command.Email), cancellationToken);

        if (user is null)
        {
            // Spend the same time hashing as a real verification would, so response times
            // do not reveal which e-mails are registered.
            _ = passwordHasher.Hash(command.Password);
            return UserErrors.InvalidCredentials;
        }

        if (!passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            return UserErrors.InvalidCredentials;
        }

        return AuthResponse.Create(user, tokenProvider.Create(user));
    }
}
