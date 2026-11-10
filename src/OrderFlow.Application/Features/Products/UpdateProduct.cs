using FluentValidation;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Products;

namespace OrderFlow.Application.Features.Products;

public sealed record UpdateProductCommand(Guid Id, string Name, string? Description, decimal Price)
    : ICommand<ProductResponse>;

internal sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(command => command.Name).ValidName();
        RuleFor(command => command.Description).ValidDescription();
        RuleFor(command => command.Price).ValidPrice();
    }
}

internal sealed class UpdateProductCommandHandler(
    IProductRepository products,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<UpdateProductCommand, ProductResponse>
{
    public async Task<Result<ProductResponse>> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(command.Id, cancellationToken);
        if (product is null)
        {
            return ProductErrors.NotFound(command.Id);
        }

        var result = product.Update(command.Name, command.Description, command.Price, timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            return result.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ProductResponse.FromDomain(product);
    }
}
