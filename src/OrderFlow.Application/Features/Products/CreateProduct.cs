using FluentValidation;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Common;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Products;

namespace OrderFlow.Application.Features.Products;

public sealed record CreateProductCommand(
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    int InitialStock) : ICommand<ProductResponse>;

internal sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(command => command.Sku).ValidSku();
        RuleFor(command => command.Name).ValidName();
        RuleFor(command => command.Description).ValidDescription();
        RuleFor(command => command.Price).ValidPrice();
        RuleFor(command => command.InitialStock).InclusiveBetween(0, ProductRules.MaxStockMovement);
    }
}

internal sealed class CreateProductCommandHandler(
    IProductRepository products,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<CreateProductCommand, ProductResponse>
{
    public async Task<Result<ProductResponse>> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var sku = Product.NormalizeSku(command.Sku);
        if (await products.SkuExistsAsync(sku, cancellationToken))
        {
            return ProductErrors.SkuAlreadyExists(sku);
        }

        var result = Product.Create(
            sku,
            command.Name,
            command.Description,
            command.Price,
            command.InitialStock,
            timeProvider.GetUtcNow());

        if (result.IsFailure)
        {
            return result.Error;
        }

        products.Add(result.Value);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            // Another request created the same SKU between the check above and this save.
            return ProductErrors.SkuAlreadyExists(sku);
        }

        return ProductResponse.FromDomain(result.Value);
    }
}
