namespace OrderFlow.Application.Abstractions.Messaging;

/// <summary>A request that changes state and returns no value.</summary>
public interface ICommand;

/// <summary>A request that changes state and returns a <typeparamref name="TResponse"/>.</summary>
public interface ICommand<TResponse>;

/// <summary>A request that only reads state.</summary>
public interface IQuery<TResponse>;
