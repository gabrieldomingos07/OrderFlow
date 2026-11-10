using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Common;

namespace OrderFlow.Application.Features.Orders;

/// <summary>Cancels a pending or paid order and returns its items to stock.</summary>
public sealed record CancelOrderCommand(Guid Id) : ICommand<OrderResponse>;

internal sealed class CancelOrderCommandHandler(
    IOrderRepository orders,
    IProductRepository products,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<CancelOrderCommand, OrderResponse>
{
    public async Task<Result<OrderResponse>> Handle(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var lookup = await orders.GetAccessibleOrderAsync(command.Id, currentUser, cancellationToken);
        if (lookup.IsFailure)
        {
            return lookup.Error;
        }

        var order = lookup.Value;
        var productIds = order.Items.Select(item => item.ProductId).ToList();
        var catalog = (await products.GetByIdsAsync(productIds, cancellationToken)).ToDictionary(product => product.Id);

        var result = order.Cancel(catalog, timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            return result.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OrderResponse.FromDomain(order);
    }
}
