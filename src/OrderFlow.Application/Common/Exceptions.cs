namespace OrderFlow.Application.Common;

/// <summary>
/// Thrown when an optimistic concurrency check fails: someone else changed the same data
/// between the moment it was read and the moment it was saved.
/// </summary>
public sealed class ConcurrencyConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>Thrown when a write would duplicate a value that must be unique (e.g. a SKU).</summary>
public sealed class UniqueConstraintViolationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
