using FluentValidation;
using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Common;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Users;

namespace OrderFlow.Application.Features.Orders;

/// <summary>Lists orders, newest first. Customers see their own orders; administrators see all.</summary>
public sealed record ListOrdersQuery(
    OrderStatus? Status,
    int Page = Pagination.DefaultPage,
    int PageSize = Pagination.DefaultPageSize) : IQuery<PagedList<OrderResponse>>;

internal sealed class ListOrdersQueryValidator : AbstractValidator<ListOrdersQuery>
{
    public ListOrdersQueryValidator()
    {
        RuleFor(query => query.Status).IsInEnum();
        RuleFor(query => query.Page).InclusiveBetween(1, Pagination.MaxPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, Pagination.MaxPageSize);
    }
}

internal sealed class ListOrdersQueryHandler(IOrderRepository orders, ICurrentUser currentUser)
    : IQueryHandler<ListOrdersQuery, PagedList<OrderResponse>>
{
    public async Task<Result<PagedList<OrderResponse>>> Handle(ListOrdersQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return UserErrors.NotAuthenticated;
        }

        var criteria = new OrderSearchCriteria(
            CustomerId: currentUser.IsAdmin ? null : userId,
            query.Status,
            query.Page,
            query.PageSize);

        var page = await orders.SearchAsync(criteria, cancellationToken);
        return page.Map(OrderResponse.FromDomain);
    }
}
