using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

internal static class ConcurrencyToken
{
    /// <summary>
    /// Uses PostgreSQL's <c>xmin</c> system column (the id of the last transaction that touched the
    /// row) as an optimistic concurrency token. It costs nothing to maintain and is mapped as a
    /// shadow property, so the domain model stays persistence-ignorant.
    /// </summary>
    public static void HasXminConcurrencyToken<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class =>
        builder.Property<uint>("Version")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();
}
