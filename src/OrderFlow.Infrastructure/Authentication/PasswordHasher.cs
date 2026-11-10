using Microsoft.AspNetCore.Identity;
using OrderFlow.Domain.Users;

namespace OrderFlow.Infrastructure.Authentication;

/// <summary>
/// Wraps ASP.NET Core Identity's hasher (PBKDF2 with HMAC-SHA512, 100,000 iterations and a
/// random salt per password) without pulling the whole Identity stack into the project.
/// </summary>
internal sealed class PasswordHasher : Application.Abstractions.Authentication.IPasswordHasher
{
    private static readonly PasswordHasher<User> Hasher = new();

    public string Hash(string password) => Hasher.HashPassword(user: null!, password);

    public bool Verify(string password, string passwordHash) =>
        Hasher.VerifyHashedPassword(user: null!, passwordHash, password)
            is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
}
