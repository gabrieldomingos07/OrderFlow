namespace OrderFlow.Domain.Common;

/// <summary>
/// Base type for every entity. Identifiers are UUIDv7, which are time-ordered and therefore
/// friendly to B-tree indexes while still being safe to generate in the application.
/// </summary>
public abstract class Entity
{
    protected Entity(Guid id) => Id = id;

    /// <summary>Parameterless constructor used by EF Core during materialization.</summary>
    protected Entity()
    {
    }

    public Guid Id { get; private init; }
}
