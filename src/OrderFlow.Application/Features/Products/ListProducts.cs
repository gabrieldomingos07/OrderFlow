using FluentValidation;
using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Common;
using OrderFlow.Domain.Common;

namespace OrderFlow.Application.Features.Products;

/// <param name="Search">Matches the name or the SKU, case-insensitively.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of products per page.</param>
/// <param name="IncludeInactive">Also return inactive products (administrators only).</param>
public sealed record ListProductsQuery(
    string? Search,
    int Page = Pagination.DefaultPage,
    int PageSize = Pagination.DefaultPageSize,
    bool IncludeInactive = false) : IQuery<PagedList<ProductResponse>>;

internal sealed class ListProductsQueryValidator : AbstractValidator<ListProductsQuery>
{
    public const int SearchMaxLength = 100;

    public ListProductsQueryValidator()
    {
        RuleFor(query => query.Search).MaximumLength(SearchMaxLength).NoNullCharacters();
        RuleFor(query => query.Page).InclusiveBetween(1, Pagination.MaxPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, Pagination.MaxPageSize);
    }
}

internal sealed class ListProductsQueryHandler(IProductRepository products, ICurrentUser currentUser)
    : IQueryHandler<ListProductsQuery, PagedList<ProductResponse>>
{
    public async Task<Result<PagedList<ProductResponse>>> Handle(ListProductsQuery query, CancellationToken cancellationToken)
    {
        var criteria = new ProductSearchCriteria(
            query.Search,
            IncludeInactive: query.IncludeInactive && currentUser.IsAdmin,
            query.Page,
            query.PageSize);

        var page = await products.SearchAsync(criteria, cancellationToken);
        return page.Map(ProductResponse.FromDomain);
    }
}
