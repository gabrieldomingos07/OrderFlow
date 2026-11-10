using OrderFlow.Domain.Users;

namespace OrderFlow.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <param name="normalizedEmail">An e-mail already normalized with <see cref="User.NormalizeEmail"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    /// <param name="normalizedEmail">An e-mail already normalized with <see cref="User.NormalizeEmail"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    void Add(User user);
}
