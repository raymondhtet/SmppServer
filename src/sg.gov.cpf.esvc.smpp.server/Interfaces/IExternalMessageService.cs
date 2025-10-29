using sg.gov.cpf.esvc.smpp.server.Models.DTOs;

namespace sg.gov.cpf.esvc.smpp.server.Interfaces;

public interface IExternalMessageService
{
    Task<ExternalServiceResult> SendMessageAsync(
        string systemId, 
        string recipientMobileNumber, 
        string message, 
        string campaignId,
        string messageId,
        CancellationToken cancellationToken);

}