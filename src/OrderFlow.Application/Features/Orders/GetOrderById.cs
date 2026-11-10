using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Common;

namespace OrderFlow.Application.Features.Orders;

public sealed record GetOrderByIdQuery(Guid Id) : IQuery<OrderResponse>;

internal sealed class GetOrderByIdQueryHandler(IOrderRepository orders, ICurrentUser currentUser)
    : IQueryHandler<GetOrderByIdQuery, OrderResponse>
{
    public async Task<Result<OrderResponse>> Handle(GetOrderByIdQuery query, CancellationToken cancellationToken)
    {
        var result = await orders.GetAccessibleOrderAsync(query.Id, currentUser, cancellationToken);
        return result.IsSuccess ? OrderResponse.FromDomain(result.Value) : result.Error;
    }
}
