using FluentValidation;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Behaviors;
using OrderFlow.Domain.Common;
using static OrderFlow.Application.UnitTests.TestData;

namespace OrderFlow.Application.UnitTests.Behaviors;

public sealed class ValidationDecoratorTests
{
    private readonly ICommandHandler<GreetCommand, string> _inner = Substitute.For<ICommandHandler<GreetCommand, string>>();

    [Fact]
    public async Task Invalid_requests_never_reach_the_handler()
    {
        var decorator = new ValidationDecorator.CommandHandler<GreetCommand, string>(_inner, [new GreetCommandValidator()]);

        var result = await decorator.Handle(new GreetCommand(""), Ct);

        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Errors.ShouldContainKey(nameof(GreetCommand.Name));
        await _inner.DidNotReceive().Handle(Arg.Any<GreetCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Valid_requests_are_delegated_to_the_handler()
    {
        _inner.Handle(Arg.Any<GreetCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success("Hello, Ana"));
        var decorator = new ValidationDecorator.CommandHandler<GreetCommand, string>(_inner, [new GreetCommandValidator()]);

        var result = await decorator.Handle(new GreetCommand("Ana"), Ct);

        result.Value.ShouldBe("Hello, Ana");
    }

    [Fact]
    public async Task Requests_without_validators_are_delegated_to_the_handler()
    {
        _inner.Handle(Arg.Any<GreetCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success("Hello"));
        var decorator = new ValidationDecorator.CommandHandler<GreetCommand, string>(_inner, []);

        var result = await decorator.Handle(new GreetCommand(""), Ct);

        result.IsSuccess.ShouldBeTrue();
    }

    public sealed record GreetCommand(string Name) : ICommand<string>;

    private sealed class GreetCommandValidator : AbstractValidator<GreetCommand>
    {
        public GreetCommandValidator() => RuleFor(command => command.Name).NotEmpty();
    }
}
