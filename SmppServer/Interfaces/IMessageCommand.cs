namespace Smpp.Server.Interfaces;

public interface IMessageCommand<TResult>
{
    Task<TResult> ExecuteAsync(CancellationToken cancellationToken = default);
}