using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Products;

namespace OrderFlow.Application.Features.Products;

public sealed record GetProductByIdQuery(Guid Id) : IQuery<ProductResponse>;

internal sealed class GetProductByIdQueryHandler(IProductRepository products, ICurrentUser currentUser)
    : IQueryHandler<GetProductByIdQuery, ProductResponse>
{
    public async Task<Result<ProductResponse>> Handle(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(query.Id, cancellationToken);

        // Inactive products are visible to administrators only.
        if (product is null || (!product.IsActive && !currentUser.IsAdmin))
        {
            return ProductErrors.NotFound(query.Id);
        }

        return ProductResponse.FromDomain(product);
    }
}
