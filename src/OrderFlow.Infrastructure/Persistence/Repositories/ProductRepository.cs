using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Common;
using OrderFlow.Domain.Products;

namespace OrderFlow.Infrastructure.Persistence.Repositories;

internal sealed class ProductRepository(OrderFlowDbContext dbContext) : IProductRepository
{
    private const string LikeEscapeCharacter = @"\";

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Products.FirstOrDefaultAsync(product => product.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default) =>
        await dbContext.Products.Where(product => ids.Contains(product.Id)).ToListAsync(cancellationToken);

    public Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken = default) =>
        dbContext.Products.AnyAsync(product => product.Sku == sku, cancellationToken);

    public Task<PagedList<Product>> SearchAsync(
        ProductSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Products.AsNoTracking();

        if (!criteria.IncludeInactive)
        {
            query = query.Where(product => product.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var pattern = $"%{QueryableExtensions.EscapeLikePattern(criteria.Search.Trim())}%";
            query = query.Where(product =>
                EF.Functions.ILike(product.Name, pattern, LikeEscapeCharacter) ||
                EF.Functions.ILike(product.Sku, pattern, LikeEscapeCharacter));
        }

        return query
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Id)
            .ToPagedListAsync(criteria.Page, criteria.PageSize, cancellationToken);
    }

    public void Add(Product product) => dbContext.Products.Add(product);
}
