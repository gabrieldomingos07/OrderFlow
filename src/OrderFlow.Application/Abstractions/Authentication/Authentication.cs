using OrderFlow.Domain.Users;

namespace OrderFlow.Application.Abstractions.Authentication;

/// <summary>The user behind the current request, if any.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    bool IsAdmin { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}

public interface ITokenProvider
{
    AccessToken Create(User user);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
