using OrderFlow.Domain.Common;

namespace OrderFlow.Domain.Products;

public static class ProductErrors
{
    public static readonly Error SkuRequired =
        Error.Validation("Products.SkuRequired", "The SKU is required.");

    public static readonly Error NameRequired =
        Error.Validation("Products.NameRequired", "The product name is required.");

    public static readonly Error InvalidPrice =
        Error.Validation("Products.InvalidPrice", "The price must be greater than zero and at most 1,000,000.");

    public static readonly Error NegativeStock =
        Error.Validation("Products.NegativeStock", "The stock quantity cannot be negative.");

    public static readonly Error InvalidQuantity =
        Error.Validation("Products.InvalidQuantity", "The quantity must be greater than zero.");

    public static readonly Error StockLimitExceeded =
        Error.Validation("Products.StockLimitExceeded", "The stock quantity would exceed the maximum supported value.");

    public static Error NotFound(Guid productId) =>
        Error.NotFound("Products.NotFound", $"Product '{productId}' was not found.");

    public static Error SkuAlreadyExists(string sku) =>
        Error.Conflict("Products.SkuAlreadyExists", $"A product with SKU '{sku}' already exists.");

    public static Error Inactive(Guid productId) =>
        Error.Conflict("Products.Inactive", $"Product '{productId}' is not available for sale.");

    public static Error InsufficientStock(Guid productId, int requested, int available) =>
        Error.Conflict(
            "Products.InsufficientStock",
            $"Product '{productId}' has {available} unit(s) in stock, but {requested} were requested.");
}
