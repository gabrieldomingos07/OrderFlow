using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Users;

namespace OrderFlow.Application.Features.Orders;

internal static class OrderAccess
{
    /// <summary>
    /// Loads an order the current user is allowed to see: customers see only their own orders,
    /// administrators see every order. Orders of other customers are reported as not found so
    /// their existence is never leaked.
    /// </summary>
    public static async Task<Result<Order>> GetAccessibleOrderAsync(
        this IOrderRepository orders,
        Guid orderId,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return UserErrors.NotAuthenticated;
        }

        var order = await orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null || (!currentUser.IsAdmin && !order.IsOwnedBy(userId)))
        {
            return OrderErrors.NotFound(orderId);
        }

        return order;
    }
}
