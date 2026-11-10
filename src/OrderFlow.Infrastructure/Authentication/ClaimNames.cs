namespace OrderFlow.Infrastructure.Authentication;

/// <summary>
/// Claim types written to and read from the access token. Inbound claim mapping is disabled,
/// so these short JWT names are what <see cref="System.Security.Claims.ClaimsPrincipal"/> exposes.
/// </summary>
internal static class ClaimNames
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
    public const string TokenId = "jti";
}
