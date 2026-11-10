using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute.ExceptionExtensions;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Behaviors;
using OrderFlow.Application.Common;
using OrderFlow.Domain.Common;
using static OrderFlow.Application.UnitTests.TestData;

namespace OrderFlow.Application.UnitTests.Behaviors;

public sealed class ConcurrencyRetryDecoratorTests
{
    private readonly ICommandHandler<BuyCommand, string> _inner = Substitute.For<ICommandHandler<BuyCommand, string>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ConcurrencyRetryDecorator.CommandHandler<BuyCommand, string> _decorator;

    public ConcurrencyRetryDecoratorTests() =>
        _decorator = new(_inner, _unitOfWork, NullLogger<ConcurrencyRetryDecorator>.Instance);

    [Fact]
    public async Task Retries_with_fresh_data_until_the_command_succeeds()
    {
        _inner.Handle(Arg.Any<BuyCommand>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => throw Conflict(),
                _ => throw Conflict(),
                _ => Task.FromResult(Result.Success("bought")));

        var result = await _decorator.Handle(new BuyCommand(), Ct);

        result.Value.ShouldBe("bought");
        await _inner.Received(3).Handle(Arg.Any<BuyCommand>(), Arg.Any<CancellationToken>());
        _unitOfWork.Received(2).DiscardPendingChanges();
    }

    [Fact]
    public async Task Gives_up_after_the_maximum_number_of_attempts()
    {
        _inner.Handle(Arg.Any<BuyCommand>(), Arg.Any<CancellationToken>()).ThrowsAsync(Conflict());

        await Should.ThrowAsync<ConcurrencyConflictException>(() => _decorator.Handle(new BuyCommand(), Ct));

        await _inner.Received(ConcurrencyRetryDecorator.MaxAttempts).Handle(Arg.Any<BuyCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Other_exceptions_are_not_retried()
    {
        _inner.Handle(Arg.Any<BuyCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UniqueConstraintViolationException("duplicate"));

        await Should.ThrowAsync<UniqueConstraintViolationException>(() => _decorator.Handle(new BuyCommand(), Ct));

        await _inner.Received(1).Handle(Arg.Any<BuyCommand>(), Arg.Any<CancellationToken>());
        _unitOfWork.DidNotReceive().DiscardPendingChanges();
    }

    [Fact]
    public async Task Business_failures_are_returned_without_retrying()
    {
        var error = Error.Conflict("Sample.Conflict", "Not possible.");
        _inner.Handle(Arg.Any<BuyCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Failure<string>(error));

        var result = await _decorator.Handle(new BuyCommand(), Ct);

        result.Error.ShouldBe(error);
        await _inner.Received(1).Handle(Arg.Any<BuyCommand>(), Arg.Any<CancellationToken>());
    }

    private static ConcurrencyConflictException Conflict() => new("Row version mismatch.");

    public sealed record BuyCommand : ICommand<string>;
}
