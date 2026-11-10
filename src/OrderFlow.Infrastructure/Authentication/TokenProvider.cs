using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Domain.Users;

namespace OrderFlow.Infrastructure.Authentication;

internal sealed class TokenProvider(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenProvider
{
    private static readonly JsonWebTokenHandler TokenHandler = new();

    public AccessToken Create(User user)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(jwt.ExpirationMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimNames.Subject, user.Id.ToString()),
                new Claim(ClaimNames.Email, user.Email),
                new Claim(ClaimNames.Name, user.FullName),
                new Claim(ClaimNames.Role, user.Role.ToString()),
                new Claim(ClaimNames.TokenId, Guid.NewGuid().ToString()),
            ]),
            SigningCredentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(TokenHandler.CreateToken(descriptor), expiresAt);
    }
}
