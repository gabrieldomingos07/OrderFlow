using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Common;
using OrderFlow.Application.Features.Products;

namespace OrderFlow.Application.UnitTests;

/// <summary>
/// Guards the reflection-based registration: every handler in the Application assembly must be
/// resolvable and must come wrapped in the validation decorator.
/// </summary>
public sealed class DependencyInjectionTests
{
    public static TheoryData<Type> HandlerInterfaces()
    {
        Type[] openHandlerTypes = [typeof(ICommandHandler<>), typeof(ICommandHandler<,>), typeof(IQueryHandler<,>)];

        var interfaces = typeof(DependencyInjection).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .SelectMany(type => type.GetInterfaces())
            .Where(type => type.IsGenericType && openHandlerTypes.Contains(type.GetGenericTypeDefinition()))
            .Distinct();

        return new TheoryData<Type>(interfaces);
    }

    [Fact]
    public void The_application_assembly_contains_handlers()
    {
        HandlerInterfaces().Count.ShouldBeGreaterThan(10);
    }

    [Theory]
    [MemberData(nameof(HandlerInterfaces))]
    public void Every_handler_resolves_wrapped_in_the_validation_decorator(Type handlerInterface)
    {
        using var provider = BuildServiceProvider();
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider.GetRequiredService(handlerInterface);

        handler.GetType().DeclaringType.ShouldBe(typeof(Application.Behaviors.ValidationDecorator));
    }

    [Fact]
    public async Task Commands_resolved_from_the_container_are_retried_after_a_concurrency_conflict()
    {
        var product = TestData.NewProduct(stock: 1);
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ => throw new ConcurrencyConflictException("stale"), _ => Task.FromResult(1));
        var products = Substitute.For<IProductRepository>();
        products.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);

        using var provider = BuildServiceProvider(unitOfWork, products);
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<RestockProductCommand, ProductResponse>>();

        var result = await handler.Handle(new RestockProductCommand(product.Id, 1), TestData.Ct);

        result.IsSuccess.ShouldBeTrue();
        await unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        unitOfWork.Received(1).DiscardPendingChanges();
    }

    private static ServiceProvider BuildServiceProvider(IUnitOfWork? unitOfWork = null, IProductRepository? products = null)
    {
        var services = new ServiceCollection();
        services.AddApplication();

        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(unitOfWork ?? Substitute.For<IUnitOfWork>());
        services.AddSingleton(products ?? Substitute.For<IProductRepository>());
        services.AddSingleton(Substitute.For<IOrderRepository>());
        services.AddSingleton(Substitute.For<IUserRepository>());
        services.AddSingleton(Substitute.For<ICurrentUser>());
        services.AddSingleton(Substitute.For<IPasswordHasher>());
        services.AddSingleton(Substitute.For<ITokenProvider>());

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
