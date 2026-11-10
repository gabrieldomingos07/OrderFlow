using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Common;

namespace OrderFlow.Infrastructure.Persistence;

/// <summary>
/// Saves changes and translates provider-specific failures into exceptions the application
/// layer understands, so nothing above this class depends on EF Core or Npgsql.
/// </summary>
internal sealed class UnitOfWork(OrderFlowDbContext dbContext) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(
                "The data was changed by another request. Reload it and try again.", exception);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
        {
            throw new UniqueConstraintViolationException(
                $"Unique constraint '{postgres.ConstraintName}' was violated.", exception);
        }
    }

    public void DiscardPendingChanges() => dbContext.ChangeTracker.Clear();
}
