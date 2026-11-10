using OrderFlow.Domain.Common;

namespace OrderFlow.Domain.Users;

public sealed class User : Entity
{
    public const int EmailMaxLength = 256;
    public const int FullNameMaxLength = 200;

    private User(Guid id, string email, string fullName, string passwordHash, UserRole role, DateTimeOffset createdAt)
        : base(id)
    {
        Email = email;
        FullName = fullName;
        PasswordHash = passwordHash;
        Role = role;
        CreatedAt = createdAt;
    }

    private User()
    {
    }

    public string Email { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static User Create(string email, string fullName, string passwordHash, UserRole role, DateTimeOffset now) =>
        new(Guid.CreateVersion7(now), NormalizeEmail(email), fullName.Trim(), passwordHash, role, now);

    /// <summary>E-mails are compared case-insensitively, so they are stored trimmed and lower-cased.</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
