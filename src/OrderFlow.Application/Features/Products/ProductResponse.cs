using OrderFlow.Domain.Products;

namespace OrderFlow.Application.Features.Products;

public sealed record ProductResponse(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    public static ProductResponse FromDomain(Product product) => new(
        product.Id,
        product.Sku,
        product.Name,
        product.Description,
        product.Price,
        product.StockQuantity,
        product.IsActive,
        product.CreatedAt,
        product.UpdatedAt);
}
