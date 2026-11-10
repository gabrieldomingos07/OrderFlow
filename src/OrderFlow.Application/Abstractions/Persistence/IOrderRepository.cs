using OrderFlow.Application.Common;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Application.Abstractions.Persistence;

public interface IOrderRepository
{
    /// <summary>Loads an order together with its items.</summary>
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedList<Order>> SearchAsync(OrderSearchCriteria criteria, CancellationToken cancellationToken = default);

    void Add(Order order);
}

/// <param name="CustomerId">When set, only orders of this customer are returned.</param>
/// <param name="Status">When set, only orders in this status are returned.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of orders per page.</param>
public sealed record OrderSearchCriteria(Guid? CustomerId, OrderStatus? Status, int Page, int PageSize);
