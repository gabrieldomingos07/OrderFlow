namespace OrderFlow.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    /// <summary>Persists every pending change in a single transaction.</summary>
    /// <exception cref="Common.ConcurrencyConflictException">Another request changed the same data first.</exception>
    /// <exception cref="Common.UniqueConstraintViolationException">A unique value is already taken.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets every entity loaded or changed so far, so the next read returns the current state of
    /// the database. Used before retrying a command that lost an optimistic concurrency race.
    /// </summary>
    void DiscardPendingChanges();
}
