namespace sg.gov.cpf.esvc.smpp.server.Interfaces;

public interface IMessageProcessor
{
    Task ProcessCompleteMessageAsync(
        string sourceAddress,
        string destinationAddress,
        string message,
        string campaignId,
        string messageId,
        int? delayInSeconds,
        ISmppSession session,
        CancellationToken cancellationToken);

}