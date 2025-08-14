namespace Smpp.Server.Interfaces;

public interface IMessageProcessor
{
    Task ProcessCompleteMessageAsync(
        string sourceAddress,
        string destinationAddress,
        string message,
        ISmppSession session,
        CancellationToken cancellationToken);

}