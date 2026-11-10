using FluentValidation;
using OrderFlow.Application.Common;
using OrderFlow.Domain.Products;

namespace OrderFlow.Application.Features.Products;

internal static class ProductRules
{
    public const int MaxStockMovement = 100_000;

    public static IRuleBuilderOptions<T, string> ValidSku<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty()
            .MaximumLength(Product.SkuMaxLength)
            .Matches("^[A-Za-z0-9-]+$").WithMessage("The SKU may contain only letters, digits and hyphens.");

    public static IRuleBuilderOptions<T, string> ValidName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(Product.NameMaxLength).NoNullCharacters();

    public static IRuleBuilderOptions<T, string?> ValidDescription<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(Product.DescriptionMaxLength).NoNullCharacters();

    public static IRuleBuilderOptions<T, decimal> ValidPrice<T>(this IRuleBuilder<T, decimal> rule) =>
        rule
            .GreaterThan(0)
            .LessThanOrEqualTo(Product.MaxPrice)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
            .WithMessage("The price must have at most 2 decimal places.");
}
