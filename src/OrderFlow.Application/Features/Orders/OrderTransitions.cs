using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Users;

namespace OrderFlow.Application.Features.Orders;

/// <summary>Simulates a successful payment for a pending order.</summary>
public sealed record PayOrderCommand(Guid Id) : ICommand<OrderResponse>;

/// <summary>Marks a paid order as shipped (administrators only).</summary>
public sealed record ShipOrderCommand(Guid Id) : ICommand<OrderResponse>;

/// <summary>Marks a shipped order as delivered (administrators only).</summary>
public sealed record DeliverOrderCommand(Guid Id) : ICommand<OrderResponse>;

internal sealed class PayOrderCommandHandler(
    IOrderRepository orders,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<PayOrderCommand, OrderResponse>
{
    public Task<Result<OrderResponse>> Handle(PayOrderCommand command, CancellationToken cancellationToken) =>
        OrderTransition.ApplyAsync(
            orders, currentUser, unitOfWork, command.Id, requiresAdmin: false,
            order => order.MarkAsPaid(timeProvider.GetUtcNow()), cancellationToken);
}

internal sealed class ShipOrderCommandHandler(
    IOrderRepository orders,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<ShipOrderCommand, OrderResponse>
{
    public Task<Result<OrderResponse>> Handle(ShipOrderCommand command, CancellationToken cancellationToken) =>
        OrderTransition.ApplyAsync(
            orders, currentUser, unitOfWork, command.Id, requiresAdmin: true,
            order => order.Ship(timeProvider.GetUtcNow()), cancellationToken);
}

internal sealed class DeliverOrderCommandHandler(
    IOrderRepository orders,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<DeliverOrderCommand, OrderResponse>
{
    public Task<Result<OrderResponse>> Handle(DeliverOrderCommand command, CancellationToken cancellationToken) =>
        OrderTransition.ApplyAsync(
            orders, currentUser, unitOfWork, command.Id, requiresAdmin: true,
            order => order.Deliver(timeProvider.GetUtcNow()), cancellationToken);
}

internal static class OrderTransition
{
    public static async Task<Result<OrderResponse>> ApplyAsync(
        IOrderRepository orders,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        Guid orderId,
        bool requiresAdmin,
        Func<Order, Result> transition,
        CancellationToken cancellationToken)
    {
        // Endpoints already enforce the role; checking again keeps the use case safe on its own.
        if (requiresAdmin && !currentUser.IsAdmin)
        {
            return UserErrors.Forbidden;
        }

        var lookup = await orders.GetAccessibleOrderAsync(orderId, currentUser, cancellationToken);
        if (lookup.IsFailure)
        {
            return lookup.Error;
        }

        var order = lookup.Value;
        var result = transition(order);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OrderResponse.FromDomain(order);
    }
}
