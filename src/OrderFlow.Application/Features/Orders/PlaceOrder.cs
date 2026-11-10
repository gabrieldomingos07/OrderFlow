using FluentValidation;
using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Products;
using OrderFlow.Domain.Users;

namespace OrderFlow.Application.Features.Orders;

public sealed record PlaceOrderCommand(IReadOnlyList<PlaceOrderItem> Items) : ICommand<OrderResponse>;

public sealed record PlaceOrderItem(Guid ProductId, int Quantity);

internal sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public const int MaxQuantityPerItem = 1_000;

    public PlaceOrderCommandValidator()
    {
        RuleFor(command => command.Items)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(items => items.Count <= Order.MaxLines)
            .WithMessage($"An order cannot contain more than {Order.MaxLines} items.")
            .Must(items => items.All(item => item is not null))
            .WithMessage("The items cannot contain null entries.")
            .Must(items => items.Select(item => item.ProductId).Distinct().Count() == items.Count)
            .WithMessage("Each product may appear only once per order.");

        RuleForEach(command => command.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).InclusiveBetween(1, MaxQuantityPerItem);
        });
    }
}

internal sealed class PlaceOrderCommandHandler(
    IProductRepository products,
    IOrderRepository orders,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<PlaceOrderCommand, OrderResponse>
{
    public async Task<Result<OrderResponse>> Handle(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } customerId)
        {
            return UserErrors.NotAuthenticated;
        }

        var productIds = command.Items.Select(item => item.ProductId).ToList();
        var catalog = (await products.GetByIdsAsync(productIds, cancellationToken)).ToDictionary(product => product.Id);

        var missingId = productIds.FirstOrDefault(id => !catalog.ContainsKey(id));
        if (missingId != Guid.Empty)
        {
            return ProductErrors.NotFound(missingId);
        }

        var lines = command.Items.Select(item => new OrderLine(catalog[item.ProductId], item.Quantity)).ToList();
        var result = Order.Place(customerId, lines, timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            return result.Error;
        }

        orders.Add(result.Value);

        // Stock changes and the new order are saved atomically. If another request changed the same
        // products in the meantime, the xmin check rejects this save and ConcurrencyRetryDecorator
        // runs the command again with fresh data.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return OrderResponse.FromDomain(result.Value);
    }
}
