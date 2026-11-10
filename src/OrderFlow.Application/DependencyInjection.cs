using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Behaviors;

namespace OrderFlow.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Decorators applied to each kind of handler, from the innermost to the outermost.
    /// Commands: validation → concurrency retry → handler. Queries: validation → handler.
    /// </summary>
    private static readonly Dictionary<Type, Type[]> DecoratorsByHandler = new()
    {
        [typeof(ICommandHandler<>)] =
            [typeof(ConcurrencyRetryDecorator.CommandHandler<>), typeof(ValidationDecorator.CommandHandler<>)],
        [typeof(ICommandHandler<,>)] =
            [typeof(ConcurrencyRetryDecorator.CommandHandler<,>), typeof(ValidationDecorator.CommandHandler<,>)],
        [typeof(IQueryHandler<,>)] =
            [typeof(ValidationDecorator.QueryHandler<,>)],
    };

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddHandlers(assembly);

        return services;
    }

    /// <summary>
    /// Registers every concrete handler of the assembly and exposes it through its handler
    /// interface, wrapped in the decorators listed in <see cref="DecoratorsByHandler"/>.
    /// </summary>
    private static void AddHandlers(this IServiceCollection services, Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });

        foreach (var implementation in handlerTypes)
        {
            foreach (var handlerInterface in implementation.GetInterfaces().Where(IsHandlerInterface))
            {
                // Factories are built once here, so resolving a handler per request involves no reflection.
                var decoratorFactories = DecoratorsByHandler[handlerInterface.GetGenericTypeDefinition()]
                    .Select(decorator => ActivatorUtilities.CreateFactory(
                        decorator.MakeGenericType(handlerInterface.GetGenericArguments()),
                        [handlerInterface]))
                    .ToArray();

                services.TryAddScoped(implementation);
                services.AddScoped(handlerInterface, provider =>
                {
                    var handler = provider.GetRequiredService(implementation);
                    foreach (var createDecorator in decoratorFactories)
                    {
                        handler = createDecorator(provider, [handler]);
                    }

                    return handler;
                });
            }
        }
    }

    private static bool IsHandlerInterface(Type type) =>
        type.IsGenericType && DecoratorsByHandler.ContainsKey(type.GetGenericTypeDefinition());
}
