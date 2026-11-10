using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Domain.Users;

namespace OrderFlow.Application.Features.Auth;

public sealed record UserResponse(Guid Id, string Email, string FullName, UserRole Role)
{
    public static UserResponse FromDomain(User user) => new(user.Id, user.Email, user.FullName, user.Role);
}

public sealed record AuthResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt, UserResponse User)
{
    public static AuthResponse Create(User user, AccessToken token) =>
        new(token.Token, "Bearer", token.ExpiresAt, UserResponse.FromDomain(user));
}
