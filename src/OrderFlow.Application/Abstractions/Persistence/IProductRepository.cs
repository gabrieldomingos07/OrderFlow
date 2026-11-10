using OrderFlow.Application.Common;
using OrderFlow.Domain.Products;

namespace OrderFlow.Application.Abstractions.Persistence;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken = default);

    Task<PagedList<Product>> SearchAsync(ProductSearchCriteria criteria, CancellationToken cancellationToken = default);

    void Add(Product product);
}

public sealed record ProductSearchCriteria(string? Search, bool IncludeInactive, int Page, int PageSize);
