using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Common;

namespace OrderFlow.Infrastructure.Persistence;

internal static class QueryableExtensions
{
    public static async Task<PagedList<T>> ToPagedListAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);

        // Validators already bound the page number; this guard keeps the method safe on its own.
        var offset = (long)(page - 1) * pageSize;
        if (offset >= totalCount)
        {
            return new PagedList<T>([], page, pageSize, totalCount);
        }

        var items = await query
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<T>(items, page, pageSize, totalCount);
    }

    /// <summary>Escapes the LIKE wildcards so user input is always matched literally.</summary>
    public static string EscapeLikePattern(string value) =>
        value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
