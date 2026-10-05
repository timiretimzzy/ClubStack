namespace ClubStack.Application.Cqrs;

public interface ICommand
{
}

public interface ICommand<out TResult> : ICommand
{
}

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand
{
    Task<TResult> Handle(TCommand command, CancellationToken cancellationToken = default);
}

public interface IQuery<out TResult>
{
}

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> Handle(TQuery query, CancellationToken cancellationToken = default);
}