using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Products;

namespace OrderFlow.Application.Features.Products;

/// <summary>
/// Removes a product from sale. Products are never hard-deleted because past orders reference them.
/// </summary>
public sealed record DeactivateProductCommand(Guid Id) : ICommand;

internal sealed class DeactivateProductCommandHandler(
    IProductRepository products,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<DeactivateProductCommand>
{
    public async Task<Result> Handle(DeactivateProductCommand command, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(command.Id, cancellationToken);
        if (product is null)
        {
            return ProductErrors.NotFound(command.Id);
        }

        product.Deactivate(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
