using OrderFlow.Api.Infrastructure;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Common;
using OrderFlow.Application.Features.Orders;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Api.Endpoints;

internal static class OrderEndpoints
{
    private const string GetOrderByIdRoute = "GetOrderById";

    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/orders")
            .WithTags("Orders")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/", Place)
            .WithName("PlaceOrder")
            .WithSummary("Places an order and reserves stock for every item.")
            .Produces<OrderResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", List)
            .WithName("ListOrders")
            .WithSummary("Lists orders, newest first. Customers only see their own orders.")
            .Produces<PagedList<OrderResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetById)
            .WithName(GetOrderByIdRoute)
            .WithSummary("Returns an order.")
            .Produces<OrderResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/pay", Pay)
            .WithName("PayOrder")
            .WithSummary("Simulates the payment of a pending order.")
            .Produces<OrderResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/ship", Ship)
            .WithName("ShipOrder")
            .WithSummary("Marks a paid order as shipped.")
            .RequireAuthorization(Policies.Admin)
            .Produces<OrderResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/deliver", Deliver)
            .WithName("DeliverOrder")
            .WithSummary("Marks a shipped order as delivered.")
            .RequireAuthorization(Policies.Admin)
            .Produces<OrderResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/cancel", Cancel)
            .WithName("CancelOrder")
            .WithSummary("Cancels a pending or paid order and returns its items to stock.")
            .Produces<OrderResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> Place(
        PlaceOrderCommand command,
        ICommandHandler<PlaceOrderCommand, OrderResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);
        return result.Match(order => Results.CreatedAtRoute(GetOrderByIdRoute, new { id = order.Id }, order));
    }

    private static async Task<IResult> List(
        IQueryHandler<ListOrdersQuery, PagedList<OrderResponse>> handler,
        CancellationToken cancellationToken,
        string? status = null,
        int page = Pagination.DefaultPage,
        int pageSize = Pagination.DefaultPageSize)
    {
        OrderStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!TryParseStatus(status, out var parsed))
            {
                return new ValidationError(new Dictionary<string, string[]>
                {
                    ["status"] = [$"The status must be one of: {string.Join(", ", Enum.GetNames<OrderStatus>())}."],
                }).ToProblem();
            }

            statusFilter = parsed;
        }

        var result = await handler.Handle(new ListOrdersQuery(statusFilter, page, pageSize), cancellationToken);
        return result.Match(Results.Ok);
    }

    /// <summary>
    /// Accepts only a single status name (case-insensitive). The default enum binding would also
    /// accept numbers and comma-separated lists, which it combines as flags: "Pending,Paid" would
    /// silently become 3, i.e. Shipped.
    /// </summary>
    private static bool TryParseStatus(string text, out OrderStatus status)
    {
        foreach (var candidate in Enum.GetValues<OrderStatus>())
        {
            if (string.Equals(candidate.ToString(), text.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                status = candidate;
                return true;
            }
        }

        status = default;
        return false;
    }

    private static async Task<IResult> GetById(
        Guid id,
        IQueryHandler<GetOrderByIdQuery, OrderResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetOrderByIdQuery(id), cancellationToken);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> Pay(
        Guid id,
        ICommandHandler<PayOrderCommand, OrderResponse> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new PayOrderCommand(id), cancellationToken)).Match(Results.Ok);

    private static async Task<IResult> Ship(
        Guid id,
        ICommandHandler<ShipOrderCommand, OrderResponse> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ShipOrderCommand(id), cancellationToken)).Match(Results.Ok);

    private static async Task<IResult> Deliver(
        Guid id,
        ICommandHandler<DeliverOrderCommand, OrderResponse> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new DeliverOrderCommand(id), cancellationToken)).Match(Results.Ok);

    private static async Task<IResult> Cancel(
        Guid id,
        ICommandHandler<CancelOrderCommand, OrderResponse> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new CancelOrderCommand(id), cancellationToken)).Match(Results.Ok);
}
