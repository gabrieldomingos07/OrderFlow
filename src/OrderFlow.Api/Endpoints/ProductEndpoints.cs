using OrderFlow.Api.Infrastructure;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Common;
using OrderFlow.Application.Features.Products;

namespace OrderFlow.Api.Endpoints;

internal static class ProductEndpoints
{
    private const string GetProductByIdRoute = "GetProductById";

    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/products").WithTags("Products");

        group.MapGet("/", List)
            .WithName("ListProducts")
            .WithSummary("Lists the catalog with search and pagination.")
            .AllowAnonymous()
            .Produces<PagedList<ProductResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetById)
            .WithName(GetProductByIdRoute)
            .WithSummary("Returns a product.")
            .AllowAnonymous()
            .Produces<ProductResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", Create)
            .WithName("CreateProduct")
            .WithSummary("Adds a product to the catalog.")
            .RequireAuthorization(Policies.Admin)
            .Produces<ProductResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", Update)
            .WithName("UpdateProduct")
            .WithSummary("Updates the name, description and price of a product.")
            .RequireAuthorization(Policies.Admin)
            .Produces<ProductResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/restock", Restock)
            .WithName("RestockProduct")
            .WithSummary("Adds units to the stock of a product.")
            .RequireAuthorization(Policies.Admin)
            .Produces<ProductResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", Deactivate)
            .WithName("DeactivateProduct")
            .WithSummary("Removes a product from sale (soft delete).")
            .RequireAuthorization(Policies.Admin)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> List(
        IQueryHandler<ListProductsQuery, PagedList<ProductResponse>> handler,
        CancellationToken cancellationToken,
        string? search = null,
        int page = Pagination.DefaultPage,
        int pageSize = Pagination.DefaultPageSize,
        bool includeInactive = false)
    {
        var result = await handler.Handle(new ListProductsQuery(search, page, pageSize, includeInactive), cancellationToken);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> GetById(
        Guid id,
        IQueryHandler<GetProductByIdQuery, ProductResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetProductByIdQuery(id), cancellationToken);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> Create(
        CreateProductCommand command,
        ICommandHandler<CreateProductCommand, ProductResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);
        return result.Match(product => Results.CreatedAtRoute(GetProductByIdRoute, new { id = product.Id }, product));
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateProductRequest request,
        ICommandHandler<UpdateProductCommand, ProductResponse> handler,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProductCommand(id, request.Name, request.Description, request.Price);
        var result = await handler.Handle(command, cancellationToken);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> Restock(
        Guid id,
        RestockProductRequest request,
        ICommandHandler<RestockProductCommand, ProductResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new RestockProductCommand(id, request.Quantity), cancellationToken);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> Deactivate(
        Guid id,
        ICommandHandler<DeactivateProductCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new DeactivateProductCommand(id), cancellationToken);
        return result.Match(Results.NoContent);
    }
}

internal sealed record UpdateProductRequest(string Name, string? Description, decimal Price);

internal sealed record RestockProductRequest(int Quantity);
