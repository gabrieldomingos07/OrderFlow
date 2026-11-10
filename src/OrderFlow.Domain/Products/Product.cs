using OrderFlow.Domain.Common;

namespace OrderFlow.Domain.Products;

/// <summary>
/// A product of the catalog. Owns its stock level and guarantees it never becomes negative.
/// </summary>
public sealed class Product : Entity
{
    public const int SkuMaxLength = 32;
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    /// <summary>
    /// Business cap on the unit price. It also guarantees that the largest possible order total
    /// fits the <c>numeric(18,2)</c> column.
    /// </summary>
    public const decimal MaxPrice = 1_000_000m;

    private Product(
        Guid id,
        string sku,
        string name,
        string? description,
        decimal price,
        int stockQuantity,
        DateTimeOffset createdAt)
        : base(id)
    {
        Sku = sku;
        Name = name;
        Description = description;
        Price = price;
        StockQuantity = stockQuantity;
        IsActive = true;
        CreatedAt = createdAt;
    }

    private Product()
    {
    }

    public string Sku { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public decimal Price { get; private set; }

    public int StockQuantity { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public static Result<Product> Create(
        string sku,
        string name,
        string? description,
        decimal price,
        int initialStock,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return ProductErrors.SkuRequired;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return ProductErrors.NameRequired;
        }

        if (price is <= 0 or > MaxPrice)
        {
            return ProductErrors.InvalidPrice;
        }

        if (initialStock < 0)
        {
            return ProductErrors.NegativeStock;
        }

        return new Product(
            Guid.CreateVersion7(now),
            NormalizeSku(sku),
            name.Trim(),
            NormalizeDescription(description),
            price,
            initialStock,
            now);
    }

    /// <summary>SKUs are case-insensitive identifiers, stored trimmed and upper-cased.</summary>
    public static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();

    public Result Update(string name, string? description, decimal price, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ProductErrors.NameRequired;
        }

        if (price is <= 0 or > MaxPrice)
        {
            return ProductErrors.InvalidPrice;
        }

        Name = name.Trim();
        Description = NormalizeDescription(description);
        Price = price;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Adds units received from a supplier.</summary>
    public Result Restock(int quantity, DateTimeOffset now) => IncreaseStock(quantity, now);

    /// <summary>Checks whether <paramref name="quantity"/> units can be reserved, without changing state.</summary>
    public Result EnsureCanReserve(int quantity)
    {
        if (!IsActive)
        {
            return ProductErrors.Inactive(Id);
        }

        if (quantity <= 0)
        {
            return ProductErrors.InvalidQuantity;
        }

        if (quantity > StockQuantity)
        {
            return ProductErrors.InsufficientStock(Id, quantity, StockQuantity);
        }

        return Result.Success();
    }

    /// <summary>Takes units out of stock for an order.</summary>
    public Result Reserve(int quantity, DateTimeOffset now)
    {
        var check = EnsureCanReserve(quantity);
        if (check.IsFailure)
        {
            return check;
        }

        StockQuantity -= quantity;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Gives back units previously reserved by an order that was cancelled.</summary>
    public Result Release(int quantity, DateTimeOffset now) => IncreaseStock(quantity, now);

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        UpdatedAt = now;
    }

    private Result IncreaseStock(int quantity, DateTimeOffset now)
    {
        if (quantity <= 0)
        {
            return ProductErrors.InvalidQuantity;
        }

        if (quantity > int.MaxValue - StockQuantity)
        {
            return ProductErrors.StockLimitExceeded;
        }

        StockQuantity += quantity;
        UpdatedAt = now;
        return Result.Success();
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
