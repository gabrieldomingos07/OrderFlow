using OrderFlow.Api.Infrastructure;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Features.Auth;

namespace OrderFlow.Api.Endpoints;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Authentication");

        group.MapPost("/register", Register)
            .WithName("Register")
            .WithSummary("Creates a customer account and returns an access token.")
            .AllowAnonymous()
            .RequireRateLimiting(Policies.AuthRateLimit)
            .Produces<AuthResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/login", Login)
            .WithName("Login")
            .WithSummary("Exchanges e-mail and password for an access token.")
            .AllowAnonymous()
            .RequireRateLimiting(Policies.AuthRateLimit)
            .Produces<AuthResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/me", Me)
            .WithName("GetCurrentUser")
            .WithSummary("Returns the profile of the authenticated user.")
            .RequireAuthorization()
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> Register(
        RegisterCommand command,
        ICommandHandler<RegisterCommand, AuthResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);
        return result.Match(response => Results.Created("/api/v1/auth/me", response));
    }

    private static async Task<IResult> Login(
        LoginCommand command,
        ICommandHandler<LoginCommand, AuthResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> Me(
        IQueryHandler<GetCurrentUserQuery, UserResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetCurrentUserQuery(), cancellationToken);
        return result.Match(Results.Ok);
    }
}
