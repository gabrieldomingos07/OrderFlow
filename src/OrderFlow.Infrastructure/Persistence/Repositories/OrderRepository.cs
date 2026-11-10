using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Common;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository(OrderFlowDbContext dbContext) : IOrderRepository
{
    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Orders
            .Include(order => order.Items)
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken);

    public Task<PagedList<Order>> SearchAsync(OrderSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Orders.AsNoTracking();

        if (criteria.CustomerId is { } customerId)
        {
            query = query.Where(order => order.CustomerId == customerId);
        }

        if (criteria.Status is { } status)
        {
            query = query.Where(order => order.Status == status);
        }

        return query
            .Include(order => order.Items)
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .ToPagedListAsync(criteria.Page, criteria.PageSize, cancellationToken);
    }

    public void Add(Order order) => dbContext.Orders.Add(order);
}
