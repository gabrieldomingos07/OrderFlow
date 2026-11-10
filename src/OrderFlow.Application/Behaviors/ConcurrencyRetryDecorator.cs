using Microsoft.Extensions.Logging;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Common;
using OrderFlow.Domain.Common;

namespace OrderFlow.Application.Behaviors;

/// <summary>
/// Re-runs a command when it loses an optimistic concurrency race. Every handler reads the state
/// it needs before changing it, so after the stale entities are discarded a retry simply works
/// with fresh data: two customers buying the same well-stocked product both succeed, while a race
/// for the last unit ends with a regular <c>Products.InsufficientStock</c> error for the loser.
/// </summary>
public sealed partial class ConcurrencyRetryDecorator
{
    public const int MaxAttempts = 5;

    private ConcurrencyRetryDecorator()
    {
    }

    public sealed class CommandHandler<TCommand>(
        ICommandHandler<TCommand> inner,
        IUnitOfWork unitOfWork,
        ILogger<ConcurrencyRetryDecorator> logger)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public Task<Result> Handle(TCommand command, CancellationToken cancellationToken) =>
            ExecuteAsync(() => inner.Handle(command, cancellationToken), unitOfWork, logger, typeof(TCommand), cancellationToken);
    }

    public sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> inner,
        IUnitOfWork unitOfWork,
        ILogger<ConcurrencyRetryDecorator> logger)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken) =>
            ExecuteAsync(() => inner.Handle(command, cancellationToken), unitOfWork, logger, typeof(TCommand), cancellationToken);
    }

    private static async Task<TResult> ExecuteAsync<TResult>(
        Func<Task<TResult>> handle,
        IUnitOfWork unitOfWork,
        ILogger logger,
        Type commandType,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await handle();
            }
            catch (ConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                LogRetry(logger, commandType.Name, attempt, MaxAttempts);
                unitOfWork.DiscardPendingChanges();

                // Small, growing, jittered pause so the competing requests do not collide again.
                var delay = TimeSpan.FromMilliseconds(Random.Shared.Next(5, 20) * attempt);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Command} lost a concurrency race (attempt {Attempt} of {MaxAttempts}); retrying with fresh data")]
    private static partial void LogRetry(ILogger logger, string command, int attempt, int maxAttempts);
}
