using FluentValidation;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Products;

namespace OrderFlow.Application.Features.Products;

/// <summary>Adds units received from a supplier to the stock of a product.</summary>
public sealed record RestockProductCommand(Guid Id, int Quantity) : ICommand<ProductResponse>;

internal sealed class RestockProductCommandValidator : AbstractValidator<RestockProductCommand>
{
    public RestockProductCommandValidator() =>
        RuleFor(command => command.Quantity).InclusiveBetween(1, ProductRules.MaxStockMovement);
}

internal sealed class RestockProductCommandHandler(
    IProductRepository products,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<RestockProductCommand, ProductResponse>
{
    public async Task<Result<ProductResponse>> Handle(RestockProductCommand command, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(command.Id, cancellationToken);
        if (product is null)
        {
            return ProductErrors.NotFound(command.Id);
        }

        var result = product.Restock(command.Quantity, timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            return result.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ProductResponse.FromDomain(product);
    }
}
