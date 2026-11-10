using FluentValidation;
using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Common;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Users;

namespace OrderFlow.Application.Features.Auth;

/// <summary>Creates a customer account and signs the new customer in.</summary>
public sealed record RegisterCommand(string Email, string FullName, string Password) : ICommand<AuthResponse>;

internal sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(User.EmailMaxLength).NoNullCharacters();
        RuleFor(command => command.FullName).NotEmpty().MaximumLength(User.FullNameMaxLength).NoNullCharacters();
        RuleFor(command => command.Password).StrongPassword();
    }
}

internal sealed class RegisterCommandHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<RegisterCommand, AuthResponse>
{
    public async Task<Result<AuthResponse>> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(command.Email);
        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            return UserErrors.EmailAlreadyInUse;
        }

        var user = User.Create(
            email,
            command.FullName,
            passwordHasher.Hash(command.Password),
            UserRole.Customer,
            timeProvider.GetUtcNow());

        users.Add(user);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            // Another request registered the same e-mail between the check above and this save.
            return UserErrors.EmailAlreadyInUse;
        }

        return AuthResponse.Create(user, tokenProvider.Create(user));
    }
}
